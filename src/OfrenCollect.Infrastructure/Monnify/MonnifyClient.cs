using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Infrastructure.Monnify;

/// <summary>
/// Concrete Monnify API client. Owns Basic→bearer authentication with token caching
/// (NFR-2.4) and server-side transaction verification (FR-3.4). The HttpClient is configured
/// (base URL, resilience) by the composition root. Contract details are grounded in
/// docs/integrations/monnify-sandbox-notes.md.
/// </summary>
public sealed class MonnifyClient : IMonnifyClient, IDisposable
{
    // Statuses that mean money actually landed and should be reconciled. PARTIALLY_PAID is
    // included: it is a real inflow that reconciles to an Underpaid invoice.
    private static readonly HashSet<string> InflowStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "PAID", "OVERPAID", "PARTIALLY_PAID" };

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private const string CurrencyCode = "NGN";
    private const string PaidOnFormat = "dd/MM/yyyy hh:mm:ss tt";
    private static readonly TimeSpan WestAfricaTimeOffset = TimeSpan.FromHours(1);
    private static readonly TimeSpan TokenExpiryBuffer = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly MonnifyOptions _options;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _authGate = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public MonnifyClient(HttpClient http, MonnifyOptions options, TimeProvider clock)
    {
        _http = http;
        _options = options;
        _clock = clock;
    }

    public void Dispose() => _authGate.Dispose();

    public async Task<VerifiedTransaction> VerifyTransactionAsync(
        string transactionReference, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var encodedReference = Uri.EscapeDataString(transactionReference);
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v2/transactions/{encodedReference}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await ReadResponseBodyAsync<VerifyTransactionBody>(response, cancellationToken);

        return new VerifiedTransaction(
            body.TransactionReference,
            Money.Of(body.AmountPaid),
            body.AccountDetails?.AccountNumber ?? string.Empty,
            ParsePaidOn(body.PaidOn),
            InflowStatuses.Contains(body.PaymentStatus));
    }

    public async Task<ReservedAccount> CreateReservedAccountAsync(
        CreateReservedAccountRequest request, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var requestBody = new CreateReservedAccountRequestBody(
            request.AccountReference,
            request.CustomerName,
            CurrencyCode,
            _options.ContractCode,
            request.CustomerEmail,
            request.CustomerName,
            GetAllAvailableBanks: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/bank-transfer/reserved-accounts")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await ReadResponseBodyAsync<ReservedAccountBody>(response, cancellationToken);
        var account = body.Accounts is { Count: > 0 } accounts
            ? accounts[0]
            : throw new MonnifyException("Monnify returned no reserved account.");

        return new ReservedAccount(account.AccountNumber, account.BankName);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && _clock.GetUtcNow() < _tokenExpiresAt)
        {
            return _cachedToken;
        }

        await _authGate.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && _clock.GetUtcNow() < _tokenExpiresAt)
            {
                return _cachedToken;
            }

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.ApiKey}:{_options.SecretKey}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await ReadResponseBodyAsync<AuthBody>(response, cancellationToken);

            _cachedToken = body.AccessToken;
            _tokenExpiresAt = _clock.GetUtcNow().AddSeconds(body.ExpiresIn) - TokenExpiryBuffer;
            return _cachedToken;
        }
        finally
        {
            _authGate.Release();
        }
    }

    private static async Task<T> ReadResponseBodyAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var envelope = await response.Content.ReadFromJsonAsync<MonnifyEnvelope<T>>(JsonOptions, cancellationToken);

        if (envelope is null || !envelope.RequestSuccessful || envelope.ResponseBody is null)
        {
            throw new MonnifyException(
                $"Monnify request was not successful: {envelope?.ResponseMessage ?? "no response body"}.");
        }

        return envelope.ResponseBody;
    }

    private DateTimeOffset ParsePaidOn(string? paidOn)
    {
        // paidOn is WAT (UTC+1) in "dd/MM/yyyy hh:mm:ss AM/PM"; timezone is unconfirmed, so
        // fall back to now if the shape differs (see monnify-sandbox-notes.md).
        if (paidOn is not null && DateTime.TryParseExact(
                paidOn, PaidOnFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return new DateTimeOffset(parsed, WestAfricaTimeOffset);
        }

        return _clock.GetUtcNow();
    }

    private sealed record MonnifyEnvelope<T>(
        [property: JsonPropertyName("requestSuccessful")] bool RequestSuccessful,
        [property: JsonPropertyName("responseMessage")] string? ResponseMessage,
        [property: JsonPropertyName("responseCode")] string? ResponseCode,
        [property: JsonPropertyName("responseBody")] T? ResponseBody);

    private sealed record AuthBody(
        [property: JsonPropertyName("accessToken")] string AccessToken,
        [property: JsonPropertyName("expiresIn")] int ExpiresIn);

    private sealed record VerifyTransactionBody(
        [property: JsonPropertyName("transactionReference")] string TransactionReference,
        [property: JsonPropertyName("amountPaid")] decimal AmountPaid,
        [property: JsonPropertyName("paymentStatus")] string PaymentStatus,
        [property: JsonPropertyName("paidOn")] string? PaidOn,
        [property: JsonPropertyName("accountDetails")] AccountDetailsBody? AccountDetails);

    private sealed record AccountDetailsBody(
        [property: JsonPropertyName("accountNumber")] string AccountNumber);

    private sealed record CreateReservedAccountRequestBody(
        [property: JsonPropertyName("accountReference")] string AccountReference,
        [property: JsonPropertyName("accountName")] string AccountName,
        [property: JsonPropertyName("currencyCode")] string CurrencyCode,
        [property: JsonPropertyName("contractCode")] string ContractCode,
        [property: JsonPropertyName("customerEmail")] string CustomerEmail,
        [property: JsonPropertyName("customerName")] string CustomerName,
        [property: JsonPropertyName("getAllAvailableBanks")] bool GetAllAvailableBanks);

    private sealed record ReservedAccountBody(
        [property: JsonPropertyName("accountReference")] string AccountReference,
        [property: JsonPropertyName("accounts")] IReadOnlyList<AccountBody>? Accounts);

    private sealed record AccountBody(
        [property: JsonPropertyName("accountNumber")] string AccountNumber,
        [property: JsonPropertyName("bankName")] string BankName);
}

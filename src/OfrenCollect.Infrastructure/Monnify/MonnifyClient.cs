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
public sealed class MonnifyClient : IMonnifyClient, IMonnifyRefundClient, IMonnifyMandateClient, IDisposable
{
    private const string MandateDateFormat = "yyyy-MM-ddTHH:mm:ss";

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

        using var response = await SendMonnifyAsync(request, cancellationToken);
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

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        var body = await ReadResponseBodyAsync<ReservedAccountBody>(response, cancellationToken);
        var account = body.Accounts is { Count: > 0 } accounts
            ? accounts[0]
            : throw new MonnifyException("Monnify returned no reserved account.");

        return new ReservedAccount(account.AccountNumber, account.BankName);
    }

    public async Task<RefundInitiationResult> InitiateRefundAsync(
        RefundInitiationRequest request, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var requestBody = new InitiateRefundRequestBody(
            request.OriginalTransactionReference,
            request.RefundReference,
            request.Amount.Amount,
            request.Reason,
            request.CustomerNote);

        // Endpoint and request/response shape confirmed against the Monnify docs (see
        // monnify-sandbox-notes.md, "Refund API"). Isolated here so any future change touches one
        // file (§2.3, §14).
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/refunds/initiate-refund")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        var body = await ReadResponseBodyAsync<RefundResponseBody>(response, cancellationToken);

        return new RefundInitiationResult(MapRefundStatus(body.RefundStatus));
    }

    public async Task<MonnifyRefundStatus> GetRefundStatusAsync(
        string refundReference, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var encodedReference = Uri.EscapeDataString(refundReference);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/refunds/{encodedReference}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        var body = await ReadResponseBodyAsync<RefundResponseBody>(response, cancellationToken);

        return MapRefundStatus(body.RefundStatus);
    }

    public async Task<MandateCreationResult> CreateMandateAsync(
        MandateCreationRequest request, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var requestBody = new CreateMandateRequestBody(
            _options.ContractCode,
            request.MandateReference,
            request.Amount.Amount,
            AutoRenew: false,
            CustomerCancellation: true,
            request.CustomerName,
            request.CustomerPhoneNumber,
            request.CustomerEmail,
            request.CustomerAddress,
            request.CustomerAccountNumber,
            request.CustomerAccountBankCode,
            request.Description,
            request.StartDate.UtcDateTime.ToString(MandateDateFormat, CultureInfo.InvariantCulture),
            request.EndDate.UtcDateTime.ToString(MandateDateFormat, CultureInfo.InvariantCulture));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/direct-debit/mandate/create")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        var body = await ReadResponseBodyAsync<CreateMandateResponseBody>(response, cancellationToken);

        return new MandateCreationResult(
            body.MandateCode ?? string.Empty,
            body.RedirectUrl ?? string.Empty,
            MapMandateStatus(body.MandateStatus));
    }

    public async Task<MonnifyMandateStatus> GetMandateStatusAsync(
        string mandateReference, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var encoded = Uri.EscapeDataString(mandateReference);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/direct-debit/mandate/?mandateReferences={encoded}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        var body = await ReadResponseBodyAsync<IReadOnlyList<MandateStatusBody>>(response, cancellationToken);

        return body is { Count: > 0 } ? MapMandateStatus(body[0].MandateStatus) : MonnifyMandateStatus.Unknown;
    }

    public async Task<string> GetMandateAuthorizationLinkAsync(
        string mandateReference, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var encoded = Uri.EscapeDataString(mandateReference);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/direct-debit/mandate/?mandateReferences={encoded}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        var body = await ReadResponseBodyAsync<IReadOnlyList<MandateStatusBody>>(response, cancellationToken);

        return body is { Count: > 0 } ? body[0].AuthorizationLink ?? string.Empty : string.Empty;
    }

    public async Task<MandateDebitResult> DebitMandateAsync(
        MandateDebitRequest request, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var requestBody = new DebitMandateRequestBody(
            request.PaymentReference, request.MandateCode, request.Amount.Amount, request.Narration, request.CustomerEmail);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/direct-debit/mandate/debit")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        var body = await ReadResponseBodyAsync<DebitMandateResponseBody>(response, cancellationToken);

        return new MandateDebitResult(body.TransactionReference ?? string.Empty, body.TransactionStatus ?? string.Empty);
    }

    public async Task<string> GetDebitStatusAsync(string paymentReference, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var encoded = Uri.EscapeDataString(paymentReference);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/direct-debit/mandate/debit-status?paymentReference={encoded}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        var body = await ReadResponseBodyAsync<DebitMandateResponseBody>(response, cancellationToken);

        return body.TransactionStatus ?? string.Empty;
    }

    public async Task CancelMandateAsync(string mandateCode, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var encoded = Uri.EscapeDataString(mandateCode);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v1/direct-debit/mandate/cancel-mandate/{encoded}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendMonnifyAsync(httpRequest, cancellationToken);
        _ = await ReadResponseBodyAsync<CancelMandateResponseBody>(response, cancellationToken);
    }

    private static MonnifyMandateStatus MapMandateStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "ACTIVE" => MonnifyMandateStatus.Active,
        "FAILED" => MonnifyMandateStatus.Failed,
        "CANCELLED" => MonnifyMandateStatus.Cancelled,
        "EXPIRED" => MonnifyMandateStatus.Expired,
        "INITIATED" or "PENDING" => MonnifyMandateStatus.Initiated,
        _ => MonnifyMandateStatus.Unknown,
    };

    private static MonnifyRefundStatus MapRefundStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "COMPLETED" => MonnifyRefundStatus.Completed,
        "FAILED" => MonnifyRefundStatus.Failed,
        _ => MonnifyRefundStatus.Pending,
    };

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

            using var response = await SendMonnifyAsync(request, cancellationToken);
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

    private async Task<HttpResponseMessage> SendMonnifyAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new MonnifyException("Could not reach Monnify.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MonnifyException("The Monnify request timed out.", exception);
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new MonnifyException($"Monnify returned HTTP {status}.");
        }

        return response;
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

    private sealed record InitiateRefundRequestBody(
        [property: JsonPropertyName("transactionReference")] string TransactionReference,
        [property: JsonPropertyName("refundReference")] string RefundReference,
        [property: JsonPropertyName("refundAmount")] decimal RefundAmount,
        [property: JsonPropertyName("refundReason")] string RefundReason,
        [property: JsonPropertyName("customerNote")] string CustomerNote);

    private sealed record RefundResponseBody(
        [property: JsonPropertyName("refundStatus")] string? RefundStatus);

    private sealed record CreateMandateRequestBody(
        [property: JsonPropertyName("contractCode")] string ContractCode,
        [property: JsonPropertyName("mandateReference")] string MandateReference,
        [property: JsonPropertyName("mandateAmount")] decimal MandateAmount,
        [property: JsonPropertyName("autoRenew")] bool AutoRenew,
        [property: JsonPropertyName("customerCancellation")] bool CustomerCancellation,
        [property: JsonPropertyName("customerName")] string CustomerName,
        [property: JsonPropertyName("customerPhoneNumber")] string CustomerPhoneNumber,
        [property: JsonPropertyName("customerEmailAddress")] string CustomerEmailAddress,
        [property: JsonPropertyName("customerAddress")] string CustomerAddress,
        [property: JsonPropertyName("customerAccountNumber")] string CustomerAccountNumber,
        [property: JsonPropertyName("customerAccountBankCode")] string CustomerAccountBankCode,
        [property: JsonPropertyName("mandateDescription")] string MandateDescription,
        [property: JsonPropertyName("mandateStartDate")] string MandateStartDate,
        [property: JsonPropertyName("mandateEndDate")] string MandateEndDate);

    private sealed record CreateMandateResponseBody(
        [property: JsonPropertyName("mandateCode")] string? MandateCode,
        [property: JsonPropertyName("mandateStatus")] string? MandateStatus,
        [property: JsonPropertyName("redirectUrl")] string? RedirectUrl);

    private sealed record MandateStatusBody(
        [property: JsonPropertyName("mandateStatus")] string? MandateStatus,
        [property: JsonPropertyName("authorizationLink")] string? AuthorizationLink);

    private sealed record DebitMandateRequestBody(
        [property: JsonPropertyName("paymentReference")] string PaymentReference,
        [property: JsonPropertyName("mandateCode")] string MandateCode,
        [property: JsonPropertyName("debitAmount")] decimal DebitAmount,
        [property: JsonPropertyName("narration")] string Narration,
        [property: JsonPropertyName("customerEmail")] string CustomerEmail);

    private sealed record DebitMandateResponseBody(
        [property: JsonPropertyName("transactionReference")] string? TransactionReference,
        [property: JsonPropertyName("transactionStatus")] string? TransactionStatus);

    private sealed record CancelMandateResponseBody(
        [property: JsonPropertyName("mandateStatus")] string? MandateStatus);
}

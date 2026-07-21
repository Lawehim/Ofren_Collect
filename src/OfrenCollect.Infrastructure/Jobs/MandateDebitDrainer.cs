using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Mandates;
using OfrenCollect.Domain.Payments;

namespace OfrenCollect.Infrastructure.Jobs;

/// <summary>
/// Drives scheduled direct debits (FR-9.3, FR-9.4): each tick it (1) auto-initiates a debit for any
/// invoice that is due under an active mandate and not yet charged, and (2) auto-reconciles pending
/// debits — applying a paid one to its invoice — so recurring billing runs hands-off. Runs with no
/// ambient tenant (like the inbox drainer), so the repositories bypass the global tenant filter.
/// Idempotent: the deterministic per-invoice reference makes Monnify dedupe retries, and it only
/// acts on still-pending debits and terminal statuses, so no invoice is ever double-charged.
/// </summary>
public sealed class MandateDebitDrainer : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 20;
    private const string PaymentReferencePrefix = "OFREN-DBT-";
    private const string Narration = "Ofren subscription debit";
    private const string PaidStatus = "PAID";
    private const string FailedStatus = "FAILED";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<MandateDebitDrainer> _logger;

    public MandateDebitDrainer(IServiceScopeFactory scopeFactory, TimeProvider clock, ILogger<MandateDebitDrainer> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await DrainAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Mandate debit drain iteration failed; will retry next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var debits = scope.ServiceProvider.GetRequiredService<IMandateDebitRepository>();
        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var payments = scope.ServiceProvider.GetRequiredService<IPaymentEventRepository>();
        var dueReader = scope.ServiceProvider.GetRequiredService<IDueMandateDebitReader>();
        var monnify = scope.ServiceProvider.GetRequiredService<IMonnifyMandateClient>();
        var notifier = scope.ServiceProvider.GetRequiredService<IReconciliationNotifier>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await InitiateDueAsync(debits, dueReader, monnify, unitOfWork, cancellationToken);

        var pending = await debits.GetPendingAsync(BatchSize, cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        var reconciled = new List<MandateDebit>();
        foreach (var debit in pending)
        {
            var status = await monnify.GetDebitStatusAsync(debit.PaymentReference, cancellationToken);
            var now = _clock.GetUtcNow();

            if (status.Equals(PaidStatus, StringComparison.OrdinalIgnoreCase))
            {
                var invoice = await invoices.GetByIdAsync(debit.InvoiceId, cancellationToken);
                if (invoice is null)
                {
                    continue;
                }

                invoice.ApplyPayment(debit.Amount);
                debit.MarkPaid(now);

                if (!await payments.ExistsByReferenceAsync(debit.TransactionReference, cancellationToken))
                {
                    payments.Add(PaymentEvent.RecordMatched(
                        debit.TenantId, debit.TransactionReference, debit.MandateReference, debit.Amount, now, invoice.Id));
                }

                reconciled.Add(debit);
            }
            else if (status.Equals(FailedStatus, StringComparison.OrdinalIgnoreCase))
            {
                debit.MarkFailed(now);
            }
        }

        // Commit the invoice payments and debit status changes together, then push live updates.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var debit in reconciled)
        {
            var invoice = await invoices.GetByIdAsync(debit.InvoiceId, cancellationToken);
            if (invoice is not null)
            {
                await notifier.PaymentReconciledAsync(
                    debit.TenantId, invoice.SubscriptionId, invoice.Id, invoice.Status, invoice.AmountPaid, cancellationToken);
            }
        }
    }

    private async Task InitiateDueAsync(
        IMandateDebitRepository debits,
        IDueMandateDebitReader dueReader,
        IMonnifyMandateClient monnify,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var due = await dueReader.GetDueAsync(now, BatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        var added = false;
        foreach (var candidate in due)
        {
            // Deterministic reference per invoice → Monnify dedupes a retried debit (§1, §10).
            var paymentReference = PaymentReferencePrefix + candidate.InvoiceId.ToString("N");

            var result = await monnify.DebitMandateAsync(
                new MandateDebitRequest(
                    paymentReference, candidate.MonnifyMandateCode, candidate.Amount, Narration, candidate.CustomerEmail),
                cancellationToken);

            var debit = MandateDebit.Initiate(
                candidate.TenantId, candidate.MandateReference, candidate.InvoiceId, paymentReference,
                result.TransactionReference, candidate.Amount, now);
            debits.Add(debit);
            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

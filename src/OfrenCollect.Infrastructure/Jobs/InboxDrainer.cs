using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Reconciliation.HandleTransactionNotification;
using OfrenCollect.Application.Refunds.ResolveRefund;
using OfrenCollect.Domain.Webhooks;

namespace OfrenCollect.Infrastructure.Jobs;

/// <summary>
/// Drains the durable webhook inbox: periodically reconciles unprocessed notifications and marks
/// them done. On restart, any notification acknowledged but not yet processed is picked up and
/// completed (NFR-2.6). Reconciliation is idempotent, so re-draining never double-counts.
/// </summary>
public sealed class InboxDrainer : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<InboxDrainer> _logger;

    public InboxDrainer(IServiceScopeFactory scopeFactory, TimeProvider clock, ILogger<InboxDrainer> logger)
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
                _logger.LogError(exception, "Inbox drain iteration failed; will retry next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInboxRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var messages = await inbox.GetUnprocessedAsync(BatchSize, cancellationToken);
        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            await DispatchAsync(mediator, message, cancellationToken);
            message.MarkProcessed(_clock.GetUtcNow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static Task DispatchAsync(ISender mediator, InboxMessage message, CancellationToken cancellationToken) =>
        message.EventType switch
        {
            WebhookEventType.TransactionCompletion => mediator.Send(
                new HandleTransactionNotificationCommand(
                    message.TransactionReference!, message.DestinationAccountNumber!),
                cancellationToken),
            WebhookEventType.RefundCompletion => mediator.Send(
                new ResolveRefundCommand(message.RefundReference!),
                cancellationToken),
            _ => Task.CompletedTask,
        };
}

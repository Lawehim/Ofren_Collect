namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>
/// The tenant-scoped queries the assistant grounds its answers on. The same tenant filtering
/// that guards every other read guards these — isolation is never delegated to the model (§9A.3).
/// </summary>
public interface IAssistantData
{
    Task<decimal> CollectedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);

    Task<int> OverdueSubscriptionCountAsync(CancellationToken cancellationToken);

    Task<int> UnderpaidInvoiceCountAsync(CancellationToken cancellationToken);

    Task<int> ActiveSubscriptionCountAsync(CancellationToken cancellationToken);
}

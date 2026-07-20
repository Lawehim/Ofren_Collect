namespace OfrenCollect.Application.Assistant;

/// <summary>
/// The fixed set of questions the assistant can answer. The language model's ONLY job is to
/// classify a question into one of these — it never sees data or produces figures. An
/// unrecognised question maps to <see cref="Unknown"/> and is declined, never guessed (FR-7.3).
/// </summary>
public enum CollectionsIntent
{
    Unknown = 0,
    CollectedThisWeek,
    OverdueCustomers,
    UnderpaidCustomers,
    ActiveSubscriptions
}

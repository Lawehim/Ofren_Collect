namespace OfrenCollect.Domain.Mandates;

/// <summary>
/// The lifecycle of a direct-debit mandate (FR-9.2). A mandate is <see cref="Pending"/> once
/// requested, becomes <see cref="Active"/> when the customer's authorisation is confirmed, and ends
/// as <see cref="Revoked"/> (by the customer or business) or <see cref="Expired"/>. Only an
/// <see cref="Active"/> mandate may be debited.
/// </summary>
public enum MandateStatus
{
    Pending = 0,
    Active,
    Revoked,
    Expired
}

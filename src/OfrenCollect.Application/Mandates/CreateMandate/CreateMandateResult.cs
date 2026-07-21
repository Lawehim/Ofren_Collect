namespace OfrenCollect.Application.Mandates.CreateMandate;

/// <summary>
/// What the caller needs after creating a mandate: our reference, the customer's authorization link
/// (send this to the customer), and the current status.
/// </summary>
public sealed record CreateMandateResult(string MandateReference, string AuthorizationLink, string Status);

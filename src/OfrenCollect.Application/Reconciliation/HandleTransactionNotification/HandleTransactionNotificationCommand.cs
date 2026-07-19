using MediatR;

namespace OfrenCollect.Application.Reconciliation.HandleTransactionNotification;

/// <summary>
/// Processes a verified Monnify transaction notification: idempotency check, server-side
/// verification, resolve to the owning subscription/invoice, apply, persist, and push the
/// result. Carries only the transaction reference — every authoritative figure comes from
/// verifying with Monnify, never from the webhook body.
/// </summary>
public sealed record HandleTransactionNotificationCommand(string TransactionReference) : IRequest;

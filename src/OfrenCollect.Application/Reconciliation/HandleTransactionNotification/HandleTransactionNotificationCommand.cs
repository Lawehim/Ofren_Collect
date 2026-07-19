using MediatR;

namespace OfrenCollect.Application.Reconciliation.HandleTransactionNotification;

/// <summary>
/// Processes a Monnify transaction notification. Carries the transaction reference and the
/// reserved account it was paid into (both read from the webhook body). Every authoritative
/// figure — amount, status — comes from re-verifying with Monnify, never from the webhook body;
/// the reserved account is used only to resolve the owning subscription (§11.3).
/// </summary>
public sealed record HandleTransactionNotificationCommand(string TransactionReference, string DestinationAccountNumber)
    : IRequest;

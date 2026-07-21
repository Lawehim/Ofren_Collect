using MediatR;

namespace OfrenCollect.Application.Mandates.CreateMandate;

/// <summary>
/// Creates a direct-debit mandate for a subscription's customer (FR-9.1). The customer's bank
/// details and consent are captured here and sent to Monnify, which returns an authorization link
/// the customer uses to authorise recurring debits.
/// </summary>
public sealed record CreateMandateCommand(
    Guid SubscriptionId,
    string CustomerAccountNumber,
    string CustomerAccountBankCode,
    string CustomerAddress,
    string CustomerPhoneNumber) : IRequest<CreateMandateResult>;

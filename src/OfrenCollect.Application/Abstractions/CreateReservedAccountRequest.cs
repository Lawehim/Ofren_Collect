namespace OfrenCollect.Application.Abstractions;

/// <summary>What Monnify needs to provision a dedicated reserved account for a subscription.</summary>
public sealed record CreateReservedAccountRequest(
    string AccountReference,
    string CustomerName,
    string CustomerEmail);

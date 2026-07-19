namespace OfrenCollect.Application.Abstractions;

/// <summary>A reserved account provisioned by Monnify for a subscription.</summary>
public sealed record ReservedAccount(string AccountNumber, string BankName);

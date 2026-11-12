namespace ToeicSpace.Payment.Application.DTOs.SePay;

public sealed record SePayWebhook(long Id, string? Gateway, string? TransactionDate,
    string? AccountNumber, string? Code, string? Content, string? TransferType,
    decimal TransferAmount, string? ReferenceCode);

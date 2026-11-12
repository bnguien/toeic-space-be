using ToeicSpace.Payment.Domain.Enums;

namespace ToeicSpace.Payment.Domain.Entities;

public sealed class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long SePayId { get; set; }
    public Guid? OrderId { get; set; }
    public string? PaymentReference { get; set; }
    public string? BankReferenceCode { get; set; }
    public string? TransferMemo { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public TransactionStatus Status { get; set; }
}

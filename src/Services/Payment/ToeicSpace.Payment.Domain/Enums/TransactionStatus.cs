namespace ToeicSpace.Payment.Domain.Enums;

public enum TransactionStatus
{
    Matched = 1,
    Unmatched = 2,
    Underpaid = 3,
    Overpaid = 4,
    Invalid = 5,
    Late = 6,
    Ambiguous = 7
}

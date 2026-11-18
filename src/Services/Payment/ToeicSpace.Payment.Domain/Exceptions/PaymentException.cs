namespace ToeicSpace.Payment.Domain.Exceptions;

public sealed class PaymentException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

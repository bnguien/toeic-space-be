using ToeicSpace.Payment.Application.DTOs.SePay;

namespace ToeicSpace.Payment.Application.Interfaces.Services;

public interface ISePayWebhookProcessor
{
    Task ProcessAsync(SePayWebhook webhook, CancellationToken ct);
}

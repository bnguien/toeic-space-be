using MediatR;
using ToeicSpace.Payment.Application.DTOs.SePay;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.SePay.Commands.ProcessSePayWebhook;

public sealed class ProcessSePayWebhookHandler(ISePayWebhookProcessor processor) : IRequestHandler<ProcessSePayWebhookCommand>
{
    public Task Handle(ProcessSePayWebhookCommand request, CancellationToken ct) =>
        processor.ProcessAsync(request.Payload, ct);
}

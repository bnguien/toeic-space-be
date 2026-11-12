using MediatR;
using ToeicSpace.Payment.Application.DTOs.SePay;

namespace ToeicSpace.Payment.Application.Features.SePay.Commands.ProcessSePayWebhook;

public sealed record ProcessSePayWebhookCommand(SePayWebhook Payload) : IRequest;

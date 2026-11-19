namespace ToeicSpace.Assessment.Application.Features.Passages.Commands.DeletePassage;

public sealed record DeletePassageCommand(Guid Id) : IRequest;

namespace ToeicSpace.Assessment.Application.Passages.Commands.DeletePassage;

public sealed record DeletePassageCommand(Guid Id) : IRequest;

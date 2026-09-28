namespace ToeicSpace.Assessment.Application.Practice.Commands.DeletePracticeSet;

public sealed record DeletePracticeSetCommand(Guid Id) : IRequest;

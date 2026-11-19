namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.DeletePracticeSet;

public sealed record DeletePracticeSetCommand(Guid Id) : IRequest;

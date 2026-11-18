namespace ToeicSpace.Assessment.Application.Features.Questions.Commands.DeleteQuestion;

public sealed record DeleteQuestionCommand(Guid Id) : IRequest;

namespace ToeicSpace.Assessment.Application.Questions.Commands.DeleteQuestion;

public sealed record DeleteQuestionCommand(Guid Id) : IRequest;

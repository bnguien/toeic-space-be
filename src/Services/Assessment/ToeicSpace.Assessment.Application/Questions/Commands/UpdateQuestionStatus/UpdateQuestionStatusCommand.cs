using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestionStatus;

public sealed record UpdateQuestionStatusCommand(
    Guid Id,
    ContentStatus Status) : IRequest<QuestionDto>;

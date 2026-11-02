using ToeicSpace.Assessment.Application.DTOs.Questions;


namespace ToeicSpace.Assessment.Application.Features.Questions.Commands.UpdateQuestionStatus;

public sealed record UpdateQuestionStatusCommand(
    Guid Id,
    ContentStatus Status) : IRequest<QuestionDto>;

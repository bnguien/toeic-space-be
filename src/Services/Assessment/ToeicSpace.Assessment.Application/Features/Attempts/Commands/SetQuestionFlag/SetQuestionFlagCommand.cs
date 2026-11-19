using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SetQuestionFlag;

/// <summary>Marks or unmarks a question for review in an in-progress attempt.</summary>
public sealed record SetQuestionFlagCommand(
    Guid AttemptId,
    Guid QuestionId,
    bool Flagged) : IRequest<QuestionFlagDto>;

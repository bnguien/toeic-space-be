using ToeicSpace.Assessment.Application.DTOs.PracticeSets;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.ReplacePracticeSetItems;

/// <param name="QuestionIds">The full ordered list of questions of the set.</param>
/// <param name="ExpectedQuestionIds">The original ordered list read by this editor.</param>
public sealed record ReplacePracticeSetItemsCommand(
    Guid Id,
    IReadOnlyList<Guid> QuestionIds,
    IReadOnlyList<Guid> ExpectedQuestionIds) : IRequest<PracticeSetDetailDto>;

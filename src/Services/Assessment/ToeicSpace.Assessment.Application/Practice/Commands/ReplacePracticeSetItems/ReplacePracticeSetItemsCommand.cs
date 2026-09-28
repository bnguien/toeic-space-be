using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;

/// <param name="QuestionIds">The full ordered list of questions of the set.</param>
public sealed record ReplacePracticeSetItemsCommand(
    Guid Id,
    IReadOnlyList<Guid> QuestionIds) : IRequest<PracticeSetDetailDto>;

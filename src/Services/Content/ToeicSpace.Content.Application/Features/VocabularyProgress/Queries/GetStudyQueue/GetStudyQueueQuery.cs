using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;

/// <summary>Exactly one source: <see cref="SetId"/>, <see cref="Due"/> = true, <see cref="FolderId"/> or <see cref="Library"/> = true.</summary>
public sealed record GetStudyQueueQuery(Guid LearnerId, Guid? SetId, bool? Due, int Limit = 20, int TzOffset = 0,
    Guid? FolderId = null, bool? Library = null) : IRequest<IReadOnlyList<StudyItemDto>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public int SourceCount =>
        (SetId.HasValue ? 1 : 0) + (Due == true ? 1 : 0) + (FolderId.HasValue ? 1 : 0) + (Library == true ? 1 : 0);
}

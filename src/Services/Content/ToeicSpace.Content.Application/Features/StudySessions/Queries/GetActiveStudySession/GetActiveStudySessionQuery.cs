using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.StudySessions.Queries.GetActiveStudySession;

/// <summary>Most recently used active session matching the optional filters, or null.</summary>
public sealed record GetActiveStudySessionQuery(Guid LearnerId, StudyMode? Mode = null, Guid? SetId = null,
    Guid? FolderId = null, bool? Library = null, bool? Due = null, int TzOffset = 0) : IRequest<StudySessionDto?>
{
    public int SourceCount => (SetId.HasValue ? 1 : 0) + (FolderId.HasValue ? 1 : 0) + (Library == true ? 1 : 0)
        + (Due == true ? 1 : 0);
}

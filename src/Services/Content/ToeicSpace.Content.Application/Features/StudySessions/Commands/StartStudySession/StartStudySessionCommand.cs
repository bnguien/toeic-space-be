using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;

/// <summary>
/// Starts a session for exactly one source, or resumes the active one for the same (mode, source) unless
/// <see cref="Restart"/> is true. A word-list session (<see cref="WordIds"/>) always starts fresh.
/// </summary>
public sealed record StartStudySessionCommand(StudyMode Mode, Guid? SetId = null, Guid? FolderId = null,
    bool? Library = null, bool? Due = null, IReadOnlyList<Guid>? WordIds = null, bool Restart = false,
    int Limit = StartStudySessionCommand.DefaultLimit, int TzOffset = 0,
    [property: JsonIgnore] Guid LearnerId = default, bool? Mistakes = null) : IRequest<StudySessionStartResult>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
    public const int MaxWords = 100;

    public int SourceCount => (SetId.HasValue ? 1 : 0) + (FolderId.HasValue ? 1 : 0) + (Library == true ? 1 : 0)
        + (Due == true ? 1 : 0) + (WordIds is not null ? 1 : 0) + (Mistakes == true ? 1 : 0);
}

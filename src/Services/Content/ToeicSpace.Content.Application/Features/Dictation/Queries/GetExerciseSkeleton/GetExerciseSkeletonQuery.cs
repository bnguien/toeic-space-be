using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseSkeleton;

/// <summary>
/// Frame of a sentence. <paramref name="Voice"/> and <paramref name="Next"/> (comma-separated ids of the next
/// sentences being played) let the server render their audio in the background.
/// </summary>
public sealed record GetExerciseSkeletonQuery(int ItemId, string? Voice, string? Next) : IRequest<DictationSkeletonDto>;

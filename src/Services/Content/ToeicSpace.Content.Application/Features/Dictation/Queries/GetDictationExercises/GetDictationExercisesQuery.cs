using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationExercises;

/// <summary>Sentences of a course (the level comes from the course) with progress and unlocks.</summary>
public sealed record GetDictationExercisesQuery(int Level, int? CourseId, Guid? LearnerId)
    : IRequest<DictationExercisesDto>;

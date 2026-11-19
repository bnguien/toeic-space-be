using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationExercises;

/// <summary>
/// A sentence is unlocked when the previous one qualifies (<see cref="DictationRules.PassesUnlock"/>); guests see
/// everything unlocked. Without a course there is nothing to list: every level is made of courses.
/// </summary>
public sealed class GetDictationExercisesHandler(IDictationCatalogRepository catalog,
    IDictationProgressRepository progressRepository)
    : IRequestHandler<GetDictationExercisesQuery, DictationExercisesDto>
{
    public async Task<DictationExercisesDto> Handle(GetDictationExercisesQuery request, CancellationToken ct)
    {
        var authenticated = request.LearnerId is not null;
        var level = DictationCatalog.IsLevel(request.Level) ? request.Level : 1;
        var course = request.CourseId is { } courseId ? await catalog.GetCourseAsync(courseId, ct) : null;
        var exercises = new List<DictationExerciseDto>();
        if (course is not null)
        {
            level = course.Level;
            var items = await catalog.GetCourseItemsAsync(course.Id, ct);
            var progress = request.LearnerId is { } learner && items.Count > 0
                ? (await progressRepository.GetProgressAsync(learner, items.Select(x => x.Id).ToArray(), ct))
                    .ToDictionary(x => x.ItemId)
                : [];
            var previousOk = true;
            foreach (var item in items)
            {
                var row = progress.GetValueOrDefault(item.Id);
                var passed = row?.Passed ?? false;
                var passedClean = row?.PassedClean ?? false;
                exercises.Add(new DictationExerciseDto(item.Id, item.Level, string.Empty, item.Order, item.Topic,
                    DictationGrader.Tokens(item.Text).Count, item.MaxPlays, !authenticated || previousOk, passed,
                    passedClean, Math.Round(row?.BestAccuracy ?? 0, 1), row?.BestScore ?? 0, row?.AttemptCount ?? 0));
                previousOk = DictationRules.PassesUnlock(item.Level, passed, passedClean);
            }
        }
        var info = DictationCatalog.LevelInfo(level);
        return new DictationExercisesDto(level, info.Name, info.Band, string.Empty, course?.ToInfoDto(),
            authenticated, exercises);
    }
}

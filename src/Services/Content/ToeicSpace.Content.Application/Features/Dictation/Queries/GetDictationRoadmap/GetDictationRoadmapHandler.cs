using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationRoadmap;

/// <summary>
/// Stage = one course of a roadmap pack (in pack order), sentences in course order. Each level has its own
/// current stage (the first not finished); later stages are <c>Locked</c> to guide the map but can still be
/// played. Stars of a finished stage come from the average best accuracy of its sentences.
/// </summary>
public sealed class GetDictationRoadmapHandler(IDictationCatalogRepository catalog,
    IDictationProgressRepository progressRepository) : IRequestHandler<GetDictationRoadmapQuery, DictationRoadmapDto>
{
    public async Task<DictationRoadmapDto> Handle(GetDictationRoadmapQuery request, CancellationToken ct)
    {
        var authenticated = request.LearnerId is not null;
        var courses = await catalog.GetCoursesAsync(DictationCatalog.RoadmapPacks.Keys.ToArray(), ct);
        var ids = await catalog.GetItemIdsAsync(courses.Select(x => x.Id).ToArray(), ct);
        var idsByCourse = ids.GroupBy(x => x.CourseId).ToDictionary(g => g.Key, g => g.Select(x => x.ItemId).ToArray());
        var progress = request.LearnerId is { } learner
            ? (await progressRepository.GetProgressAsync(learner, null, ct)).ToDictionary(x => x.ItemId)
            : [];

        var levels = new List<DictationRoadmapLevelDto>();
        int totalItems = 0, totalPassed = 0;
        foreach (var info in DictationCatalog.Levels)
        {
            var stages = new List<DictationStageDto>();
            var frontierFound = false;
            foreach (var course in courses.Where(x => x.Level == info.Level))
            {
                if (!idsByCourse.TryGetValue(course.Id, out var itemIds) || itemIds.Length == 0) continue;
                var rows = itemIds.Select(id => progress.GetValueOrDefault(id)).Where(x => x is not null).ToArray();
                var passed = rows.Count(x => x!.Passed);
                var done = authenticated && passed == itemIds.Length;
                DictationStageState state;
                if (done) state = DictationStageState.Done;
                else if (!frontierFound) { state = DictationStageState.Current; frontierFound = true; }
                else state = DictationStageState.Locked;
                stages.Add(new DictationStageDto(stages.Count + 1, course.Id, course.Title, itemIds, itemIds.Length,
                    passed, rows.Count(x => x!.PassedClean),
                    done ? DictationRules.StageStars(rows.Select(x => x!.BestAccuracy).ToArray()) : 0, state,
                    string.Empty, [course.Title], course.WordsPerSentence, course.VocabCount));
            }
            var levelItems = stages.Sum(x => x.ItemCount);
            var levelPassed = stages.Sum(x => x.PassedCount);
            totalItems += levelItems;
            totalPassed += levelPassed;
            levels.Add(new DictationRoadmapLevelDto(info.Level, info.Name,
                DictationCatalog.RoadmapBands.GetValueOrDefault(info.Level, info.Band), info.Cefr, levelItems,
                levelPassed, stages.Count(x => x.State == DictationStageState.Done), stages.Sum(x => x.Stars), stages));
        }
        return new DictationRoadmapDto(authenticated, totalItems, totalPassed, levels);
    }
}

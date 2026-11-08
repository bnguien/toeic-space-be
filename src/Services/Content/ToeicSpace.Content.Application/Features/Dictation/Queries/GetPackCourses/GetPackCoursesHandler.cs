using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetPackCourses;

public sealed class GetPackCoursesHandler(IDictationCatalogRepository catalog,
    IDictationProgressRepository progressRepository) : IRequestHandler<GetPackCoursesQuery, PackCoursesDto>
{
    public async Task<PackCoursesDto> Handle(GetPackCoursesQuery request, CancellationToken ct)
    {
        var pack = request.Pack!.Trim();
        var courses = await catalog.GetCoursesAsync([pack], ct);
        var passed = request.LearnerId is { } learner && courses.Count > 0
            ? await progressRepository.CountPassedByCourseAsync(learner, courses.Select(x => x.Id).ToArray(), ct)
            : new Dictionary<int, int>();
        return new PackCoursesDto(pack, courses
            .OrderBy(x => x.Order).ThenBy(x => x.Id)
            .Select(x => new PackCourseDto(x.Id, x.Code, x.Title, x.Order, x.Level, x.ItemCount,
                passed.GetValueOrDefault(x.Id)))
            .ToArray());
    }
}

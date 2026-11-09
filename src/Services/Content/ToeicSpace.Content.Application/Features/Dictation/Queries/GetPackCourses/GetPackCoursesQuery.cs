using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetPackCourses;

/// <summary>Courses of a pack in pack order with the learner's passed sentences (no sentence content).</summary>
public sealed record GetPackCoursesQuery(string? Pack, Guid? LearnerId) : IRequest<PackCoursesDto>;

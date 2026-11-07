using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationRoadmap;

/// <summary>The 5 roadmap levels cut into stages with the learner's progress (null = guest).</summary>
public sealed record GetDictationRoadmapQuery(Guid? LearnerId) : IRequest<DictationRoadmapDto>;

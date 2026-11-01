using ToeicSpace.Assessment.Application.DTOs.PracticeSets;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Queries.GetPracticeSetContent;

/// <summary>
/// Practice content always includes the answer key and explanations so learners get
/// immediate feedback; full tests never expose them to learners.
/// </summary>
public sealed record GetPracticeSetContentQuery(Guid Id) : IRequest<PracticeSetContentDto>;

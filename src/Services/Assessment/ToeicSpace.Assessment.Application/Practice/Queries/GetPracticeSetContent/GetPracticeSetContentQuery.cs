using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetContent;

/// <summary>
/// Practice content always includes the answer key and explanations so learners get
/// immediate feedback; full tests never expose them to learners.
/// </summary>
public sealed record GetPracticeSetContentQuery(Guid Id) : IRequest<PracticeSetContentDto>;

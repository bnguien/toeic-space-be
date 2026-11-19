using ToeicSpace.Assessment.Application.DTOs.PracticeSets;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Queries.GetPracticeSetContent;

/// <summary>
/// Practice set preview. Content managers get the answer key, explanations, transcripts and translations;
/// learners get the same content without them (answers are revealed through attempts only).
/// </summary>
public sealed record GetPracticeSetContentQuery(Guid Id) : IRequest<PracticeSetContentDto>;

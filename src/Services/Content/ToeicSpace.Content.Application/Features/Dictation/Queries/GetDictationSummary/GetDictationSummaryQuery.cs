using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationSummary;

public sealed record GetDictationSummaryQuery(Guid? LearnerId) : IRequest<DictationSummaryDto>;

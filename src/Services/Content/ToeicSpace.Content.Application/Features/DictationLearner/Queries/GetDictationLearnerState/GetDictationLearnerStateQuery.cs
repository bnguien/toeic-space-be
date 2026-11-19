using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.DictationLearner.Queries.GetDictationLearnerState;

public sealed record GetDictationLearnerStateQuery(Guid LearnerId) : IRequest<DictationLearnerStateDto>;

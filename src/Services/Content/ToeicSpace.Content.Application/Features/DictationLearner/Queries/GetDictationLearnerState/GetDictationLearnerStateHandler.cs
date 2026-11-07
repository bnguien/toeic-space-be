using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.DictationLearner.Queries.GetDictationLearnerState;

/// <summary>The learner's stored state, or empty defaults when nothing was saved yet.</summary>
public sealed class GetDictationLearnerStateHandler(IDictationProgressRepository repository)
    : IRequestHandler<GetDictationLearnerStateQuery, DictationLearnerStateDto>
{
    public async Task<DictationLearnerStateDto> Handle(GetDictationLearnerStateQuery request, CancellationToken ct) =>
        (await repository.GetStateAsync(request.LearnerId, false, ct)).ToDto();
}

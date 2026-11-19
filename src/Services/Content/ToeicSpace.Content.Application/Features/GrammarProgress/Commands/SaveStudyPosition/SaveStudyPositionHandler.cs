using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.SaveStudyPosition;

public sealed class SaveStudyPositionHandler(GrammarContentProvider content, IGrammarLearningRepository repository,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<SaveStudyPositionCommand, PositionResultDto>
{
    public async Task<PositionResultDto> Handle(SaveStudyPositionCommand request, CancellationToken ct)
    {
        var topic = await content.GetTopicAsync(request.Topic, ct);
        var state = await GrammarLearnerStates.GetOrCreateAsync(repository, request.LearnerId, ct);
        state.PositionTopic = topic.Topic;
        state.PositionPart = request.Part;
        state.PositionAnchor = (request.Anchor ?? string.Empty).Trim();
        state.Touch(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        return new PositionResultDto(new StudyPositionDto(state.PositionTopic, state.PositionPart, state.PositionAnchor));
    }
}

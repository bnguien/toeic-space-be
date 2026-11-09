using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.SetKnownWord;

public sealed class SetKnownWordHandler(GrammarContentProvider content, IGrammarLearningRepository repository,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<SetKnownWordCommand, KnownWordResultDto>
{
    public async Task<KnownWordResultDto> Handle(SetKnownWordCommand request, CancellationToken ct)
    {
        var topic = await content.GetTopicAsync(request.Topic, ct);
        if (!topic.VocabWords.Contains(request.Word))
            throw GrammarErrors.Invalid("This word is not in the topic's vocabulary.");
        var state = await GrammarLearnerStates.GetOrCreateAsync(repository, request.LearnerId, ct);
        if (state.SetKnownWord(topic.Topic, request.Word, request.Known))
        {
            state.Touch(clock.GetUtcNow().UtcDateTime);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return new KnownWordResultDto(topic.Topic, request.Word, request.Known);
    }
}

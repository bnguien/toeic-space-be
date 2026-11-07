using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.CompleteTopicPart;

public sealed class CompleteTopicPartHandler(GrammarContentProvider content, IGrammarLearningRepository repository,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<CompleteTopicPartCommand, CompletePartResultDto>
{
    public async Task<CompletePartResultDto> Handle(CompleteTopicPartCommand request, CancellationToken ct)
    {
        var topic = await content.GetTopicAsync(request.Topic, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var progress = await repository.GetLessonProgressAsync(request.LearnerId, topic.Topic, ct);
        if (progress is null)
        {
            progress = new GrammarLessonProgress { LearnerId = request.LearnerId, Topic = topic.Topic };
            repository.AddLessonProgress(progress);
        }
        var changed = false;
        if (request.Part == CompleteTopicPartCommand.Theory && progress.TheoryConfirmedAt is null)
        {
            progress.TheoryConfirmedAt = now;
            changed = true;
        }
        else if (request.Part == CompleteTopicPartCommand.Vocab && progress.VocabConfirmedAt is null)
        {
            progress.VocabConfirmedAt = now;
            changed = true;
        }
        if (changed)
        {
            GrammarProgressService.MarkCompletedIfDone(topic, progress, now);
            progress.Touch(now);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return new CompletePartResultDto(GrammarProgressService.LessonDto(topic, progress));
    }
}

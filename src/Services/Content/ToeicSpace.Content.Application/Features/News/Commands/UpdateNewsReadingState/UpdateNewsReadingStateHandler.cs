using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.News.Commands.UpdateNewsReadingState;

public sealed class UpdateNewsReadingStateHandler(NewsReadingService service, IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<UpdateNewsReadingStateCommand, NewsReadingStateDto>
{
    public async Task<NewsReadingStateDto> Handle(UpdateNewsReadingStateCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var article = await service.GetArticleAsync(request.Slug, ct);
        var reading = await service.GetOrOpenAsync(request.LearnerId, article, now, ct);

        if (request.ReadingSeconds is { } seconds) reading.AddReadingTime(seconds, now);
        if (request.KnownWords is not null)
        {
            var vocabulary = article.Vocabulary.Select(x => x.Word).ToHashSet();
            reading.SetKnownWords(request.KnownWords.Where(vocabulary.Contains), now);
        }
        if (request.Notes is not null) reading.SetNotes(request.Notes, now);
        if (request.ResetDictation) reading.ResetDictation(now);
        foreach (var (sentenceId, entry) in request.Dictation ?? new Dictionary<string, NewsDictationEntryInput>())
            if (article.FindSentence(sentenceId) is not null) reading.MergeDictation(sentenceId, entry.ToEntity(), now);
        if (request.Settings is not null) reading.SetSettings(request.Settings.ToEntity(), now);

        await unitOfWork.SaveChangesAsync(ct);
        return await service.PresentAsync(reading, article, ct);
    }
}

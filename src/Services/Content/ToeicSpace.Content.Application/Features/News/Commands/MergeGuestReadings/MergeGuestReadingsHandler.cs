using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.News.Commands.MergeGuestReadings;

public sealed class MergeGuestReadingsHandler(INewsArticleRepository articles, INewsReadingRepository readings,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<MergeGuestReadingsCommand, NewsMergeResultDto>
{
    public async Task<NewsMergeResultDto> Handle(MergeGuestReadingsCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var items = request.Items.GroupBy(x => x.Slug).Select(x => x.Last()).ToList();
        var found = (await articles.GetPublishedBySlugsAsync(items.Select(x => x.Slug).ToArray(), ct))
            .ToDictionary(x => x.Slug);
        var existing = (await readings.GetForArticlesForUpdateAsync(request.LearnerId,
            found.Values.Select(x => x.Id).ToArray(), ct)).ToDictionary(x => x.ArticleId);

        var merged = 0;
        foreach (var item in items)
        {
            if (!found.TryGetValue(item.Slug, out var article)) continue;
            var isNew = !existing.TryGetValue(article.Id, out var reading);
            if (reading is null)
            {
                reading = NewsReading.Open(request.LearnerId, article.Id, now);
                readings.Add(reading);
            }
            var guestIsNewer = isNew || (item.UpdatedAt?.UtcDateTime ?? DateTime.MinValue) > reading.UpdatedAt;
            reading.AddReadingTime(item.ReadingSeconds, now);
            if (item.Completed) reading.Complete(now);
            if (guestIsNewer && item.KnownWords is not null)
            {
                var vocabulary = article.Vocabulary.Select(x => x.Word).ToHashSet();
                reading.SetKnownWords(item.KnownWords.Where(vocabulary.Contains), now);
            }
            if (guestIsNewer && item.Notes is not null) reading.SetNotes(item.Notes, now);
            foreach (var (sentenceId, entry) in item.Dictation ?? new Dictionary<string, NewsDictationEntryInput>())
                if (article.FindSentence(sentenceId) is not null)
                    reading.MergeDictation(sentenceId, entry.ToEntity(), now);
            merged++;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new NewsMergeResultDto(merged, items.Count - merged);
    }
}

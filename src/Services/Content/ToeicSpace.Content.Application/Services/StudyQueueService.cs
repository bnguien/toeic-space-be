using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Services;

/// <summary>Resolves the words of a study source and builds study items; shared by the study queue and sessions.</summary>
public sealed class StudyQueueService(ILearnerProgressRepository progress, IVocabularyCatalogRepository catalog,
    ILearnerLibraryRepository library, PersonalWordOverlay overlay)
{
    /// <summary>Source title of a mistakes session.</summary>
    public const string MistakesTitle = "Từ sai";

    public sealed record Source(StudySourceKind Kind, Guid? Id, string? Title, IReadOnlyList<Guid> WordIds);

    /// <summary>Kind and id from the exclusive request fields (validators guarantee exactly one is set).</summary>
    public static (StudySourceKind Kind, Guid? Id) KindOf(Guid? setId, Guid? folderId, bool? library, bool? due,
        IReadOnlyList<Guid>? wordIds, bool? mistakes = null) =>
        setId is { } set ? (StudySourceKind.Set, set)
        : folderId is { } folder ? (StudySourceKind.Folder, folder)
        : library == true ? (StudySourceKind.Library, null)
        : due == true ? (StudySourceKind.Due, null)
        : mistakes == true ? (StudySourceKind.Mistakes, null)
        : wordIds is not null ? (StudySourceKind.Words, null)
        : throw new ArgumentException("No study source.");

    /// <summary>
    /// Set: every word in lesson order. Folder/Library: every saved word, newest first. Due: due words by due date,
    /// then oldest review, at most <paramref name="limit"/>. Mistakes: every pending mistake, most recent first.
    /// Words: the given words in order.
    /// </summary>
    public async Task<Source> ResolveAsync(Guid learnerId, StudySourceKind kind, Guid? sourceId,
        IReadOnlyList<Guid>? wordIds, int limit, DateOnly today, CancellationToken ct)
    {
        switch (kind)
        {
            case StudySourceKind.Set:
                var set = await catalog.GetSetDetailAsync(sourceId!.Value, ct) ?? throw VocabularyErrors.SetNotFound();
                return new Source(kind, set.Id, set.Title, set.Words.OrderBy(x => x.Order).Select(x => x.WordId).ToArray());
            case StudySourceKind.Folder or StudySourceKind.Library:
                string? title = null;
                if (kind == StudySourceKind.Folder)
                    title = (await library.GetFoldersAsync(learnerId, ct)).FirstOrDefault(x => x.Id == sourceId)?.Name
                        ?? throw VocabularyErrors.FolderNotFound();
                var saved = (await library.GetSavedWordsAsync(learnerId, ct))
                    .Where(x => kind == StudySourceKind.Library || x.FolderId == sourceId)
                    .OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).Select(x => x.WordId).ToArray();
                return new Source(kind, sourceId, title, saved);
            case StudySourceKind.Due:
                var due = await progress.GetDueAsync(learnerId, today, limit, ct);
                return new Source(kind, null, null, due.Select(x => x.WordId).ToArray());
            case StudySourceKind.Mistakes:
                return new Source(kind, null, MistakesTitle, await progress.GetMistakeWordIdsAsync(learnerId, ct));
            default:
                var ids = wordIds!.Distinct().ToArray();
                if ((await catalog.GetWordsAsync(ids, learnerId, ct)).Count != ids.Length)
                    throw VocabularyErrors.WordNotFound();
                return new Source(kind, null, null, ids);
        }
    }

    /// <summary>
    /// One study item per id, in order (repeated ids allowed), with the learner's personal edits; words the learner
    /// cannot see are skipped.
    /// </summary>
    public async Task<IReadOnlyList<StudyItemDto>> BuildItemsAsync(Guid learnerId, IReadOnlyList<Guid> wordIds,
        DateOnly today, CancellationToken ct)
    {
        var distinct = wordIds.Distinct().ToArray();
        var words = (await catalog.GetWordsAsync(distinct, learnerId, ct)).ToDictionary(x => x.Id);
        var rows = (await progress.GetForWordsAsync(learnerId, distinct, ct)).ToDictionary(x => x.WordId);
        var saved = await overlay.LoadAsync(learnerId, distinct, ct);
        return wordIds.Where(words.ContainsKey).Select(id => words[id].ToStudyItemDto(rows.GetValueOrDefault(id), today,
            saved.GetValueOrDefault(id))).ToArray();
    }
}

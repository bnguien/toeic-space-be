using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Mappings;

public static class NewsMappingExtensions
{
    /// <summary>Gateway URL of a stored cover, or null.</summary>
    public static string? CoverUrl(this NewsSettings settings, string? coverFile) =>
        string.IsNullOrEmpty(coverFile) ? null : $"{settings.CoverBaseUrl.TrimEnd('/')}/{coverFile}";

    public static NewsArticleCardDto ToCardDto(this NewsArticleSummary x, string? coverUrl, NewsCardProgress? progress) =>
        new(x.Slug, x.TitleEn, x.TitleVi, x.SummaryVi, x.SourceName, x.PublishedAt, x.Stage, x.Topic, x.Minutes,
            x.WordCount, coverUrl, x.CoverAlt, x.Featured, progress?.Done ?? false, progress?.ReadingSeconds);

    public static NewsArticleDetailDto ToDetailDto(this NewsArticle x, string? coverUrl) =>
        new(x.Slug, x.TitleEn, x.TitleVi, x.SummaryVi, x.Stage, x.Topic, x.Minutes, x.WordCount, x.SourceName,
            x.SourceUrl, x.SourceSeries, x.Author, x.PublishedAt, x.License, x.Attribution, coverUrl, x.CoverAlt,
            x.CoverCredit, x.CoverLicense, x.CoverSourceUrl, x.AudioUrl,
            x.Paragraphs.Select(p => new NewsParagraphDto(p.Id,
                p.Sentences.Select(s => new NewsSentenceDto(s.Id, s.Text, s.Translation)).ToList())).ToList(),
            x.Vocabulary.Select(v => new NewsVocabularyItemDto(v.Word, v.Ipa, v.PartOfSpeech, v.Meaning, v.Level)).ToList());

    public static DictationResultDto ToDto(this NewsDictationGrade x, bool recorded) =>
        new(x.Correct, x.Words.Select(w => new DictationWordDto(w.Text, w.Ok)).ToList(), x.CorrectCount, x.Total,
            x.Extra, recorded);

    /// <summary>State of a reading; the per-word marks of each last dictation input are recomputed.</summary>
    public static NewsReadingStateDto ToStateDto(this NewsReading r, NewsArticle article, NewsReaderSettings? settings,
        DateTime? settingsUpdatedAt)
    {
        var dictation = r.Dictation.ToDictionary(x => x.Key, x =>
        {
            var answer = article.FindSentence(x.Key)?.Text;
            var result = x.Value.Input is null || answer is null
                ? null
                : NewsDictationGrader.Grade(x.Value.Input, answer).ToDto(recorded: true);
            return new NewsDictationEntryDto(x.Value.Passed, x.Value.Unaided, x.Value.Revealed, x.Value.Attempts,
                x.Value.Input, result);
        });
        return new NewsReadingStateDto(article.Slug, r.IsDone, r.OpenedAt, r.CompletedAt, r.ReadingSeconds,
            r.KnownWords, r.Notes ?? string.Empty, dictation, article.Sentences.Count(), settings?.ToDto(),
            settingsUpdatedAt, r.UpdatedAt);
    }

    public static NewsReaderSettingsDto ToDto(this NewsReaderSettings x) =>
        new(x.Palette, x.Mode, x.Layout, x.FontSize, x.Dark, x.PanelWidth, x.Split, x.ShowSuggested, x.ShowLookedUp,
            x.ShowMeaning, x.HackMode, x.Voice);

    public static NewsReaderSettings ToEntity(this NewsReaderSettingsDto x) => new()
    {
        Palette = x.Palette, Mode = x.Mode, Layout = x.Layout, FontSize = x.FontSize, Dark = x.Dark,
        PanelWidth = x.PanelWidth, Split = x.Split, ShowSuggested = x.ShowSuggested, ShowLookedUp = x.ShowLookedUp,
        ShowMeaning = x.ShowMeaning, HackMode = x.HackMode, Voice = x.Voice
    };

    public static NewsDictationEntry ToEntity(this NewsDictationEntryInput x) => new()
    {
        Passed = x.Passed, Unaided = x.Unaided, Revealed = x.Revealed, Attempts = x.Attempts, Input = x.Input
    };
}

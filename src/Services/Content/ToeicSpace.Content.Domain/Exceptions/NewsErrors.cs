namespace ToeicSpace.Content.Domain.Exceptions;

/// <summary>Error codes of the bilingual news feature (ProblemDetails <c>code</c>).</summary>
public static class NewsErrors
{
    public static ContentRuleException ArticleNotFound() =>
        new("news_article_not_found", "News article not found.", 404);

    public static ContentRuleException SentenceNotFound() =>
        new("news_sentence_not_found", "This sentence is not in the article.", 404);

    public static ContentRuleException CoverNotFound() =>
        new("news_cover_not_found", "Cover image not found.", 404);

    public static ContentRuleException InvalidImportFile(string fileName, string reason) =>
        new("invalid_import_file", $"{fileName}: {reason}");
}

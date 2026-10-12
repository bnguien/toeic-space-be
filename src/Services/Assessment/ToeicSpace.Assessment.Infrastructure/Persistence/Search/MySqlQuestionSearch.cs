using System.Text.RegularExpressions;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Domain.Entities;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Search;

/// <summary>
/// Keyword search backed by the FT_ToeicQuestions_Content FULLTEXT index (question text,
/// explanation, transcript). Every word must match as a prefix, e.g. "meet sched" finds
/// "meeting schedule". Words shorter than the InnoDB minimum token size are ignored.
/// </summary>
public sealed partial class MySqlQuestionSearch : IQuestionSearch
{
    private const int MinTokenLength = 3;
    private const int MaxTokens = 8;

    public IQueryable<ToeicQuestion> Apply(
        IQueryable<ToeicQuestion> query,
        string searchTerm)
    {
        var tokens = WordRegex()
            .Matches(searchTerm)
            .Select(match => match.Value)
            .Where(token => token.Length >= MinTokenLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTokens)
            .ToList();

        if (tokens.Count == 0)
        {
            // Too short for the FULLTEXT index: fall back to the small indexed columns only.
            var term = searchTerm.Trim();
            return query.Where(question =>
                (question.Topic != null && question.Topic.Contains(term))
                || (question.QuestionText != null && question.QuestionText.Contains(term)));
        }

        var booleanQuery = string.Join(' ', tokens.Select(token => $"+{token}*"));

        return query.Where(question => EF.Functions.IsMatch(
            new[] { question.QuestionText, question.Explanation, question.Transcript },
            booleanQuery,
            MySqlMatchSearchMode.Boolean));
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}

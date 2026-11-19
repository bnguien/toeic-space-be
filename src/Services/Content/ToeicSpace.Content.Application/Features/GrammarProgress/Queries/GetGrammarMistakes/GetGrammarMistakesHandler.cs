using System.Text.RegularExpressions;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarMistakes;

public sealed class GetGrammarMistakesHandler(GrammarContentProvider content, IGrammarLearningRepository repository)
    : IRequestHandler<GetGrammarMistakesQuery, IReadOnlyList<MistakeGroupDto>>
{
    private static readonly Regex Unit = new(@"^(CĐ\d{2}\.\d+)", RegexOptions.Compiled);

    /// <summary>"CĐ01.1.2" → "CĐ01.1" (unit); an invalid code is kept.</summary>
    public static string UnitOf(string code) => Unit.Match(code) is { Success: true } m ? m.Groups[1].Value : code;

    public async Task<IReadOnlyList<MistakeGroupDto>> Handle(GetGrammarMistakesQuery request, CancellationToken ct)
    {
        var bundle = await content.GetAsync(ct);
        var rows = await repository.ListMistakesAsync(request.LearnerId, GetGrammarMistakesQuery.MaxQuestions, ct);
        var groups = new Dictionary<string, (string Topic, List<MistakeQuestionDto> Questions)>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!bundle.Questions.TryGetValue(row.QuestionId, out var question)) continue; // removed from the content
            var unit = UnitOf(string.IsNullOrEmpty(row.UnitCode) ? question.UnitCode : row.UnitCode);
            if (!groups.TryGetValue(unit, out var group))
                groups[unit] = group = (unit.StartsWith("CĐ") && unit.Length >= 4 ? unit[2..4] : question.MainTopic, []);
            group.Questions.Add(new MistakeQuestionDto(row.QuestionId, row.Block, row.WrongCount, question.Topic, question.Stage));
        }
        return groups.OrderBy(g => SortKey(g.Key), Comparer<int[]>.Create(Compare))
            .Select(g => new MistakeGroupDto(g.Key, bundle.UnitTitles.GetValueOrDefault(g.Key, string.Empty), g.Value.Topic,
                g.Value.Questions))
            .ToList();
    }

    private static int[] SortKey(string code)
    {
        if (!code.StartsWith("CĐ")) return [999];
        var parts = code[2..].Split('.');
        return parts.All(p => int.TryParse(p, out _)) ? parts.Select(int.Parse).ToArray() : [999];
    }

    private static int Compare(int[] a, int[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        return a.Length.CompareTo(b.Length);
    }
}

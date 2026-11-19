using System.Text.Json;
using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Common.Querying;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Reads the published content an attempt is started from. Starting reads it twice: once before media
/// preservation (outside locks) and once under the content locks; the two reads must be identical.
/// </summary>
public sealed class AttemptSourceReader
{
    private readonly IAssessmentDbContext _context;

    public AttemptSourceReader(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task<AttemptSourceContent> ReadTestAsync(
        Guid testId,
        IReadOnlyCollection<ToeicPart> parts,
        CancellationToken cancellationToken)
    {
        var test = await _context.ToeicTests
            .AsNoTracking()
            .VisibleTo(canManageContent: false)
            .SingleOrDefaultAsync(item => item.Id == testId, cancellationToken)
            ?? throw AppException.NotFound("Published test is unavailable.");

        var questions = await _context.ToeicQuestions
            .AsNoTracking()
            .Where(question => question.TestId == testId
                && question.Status == ContentStatus.Active
                && parts.Contains(question.Part))
            .ToListAsync(cancellationToken);

        var order = questions
            .OrderBy(question => question.QuestionNumber ?? int.MaxValue)
            .ThenBy(question => question.OrderIndex)
            .ThenBy(question => question.Id)
            .Select(question => question.Id)
            .ToList();

        return new AttemptSourceContent(test, null, questions.OrderBy(question => question.Id).ToList(),
            await PassagesAsync(questions, cancellationToken), order);
    }

    public async Task<AttemptSourceContent> ReadPracticeSetAsync(Guid practiceSetId, CancellationToken cancellationToken)
    {
        var set = await _context.ToeicPracticeSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == practiceSetId && item.Status == ContentStatus.Active, cancellationToken)
            ?? throw AppException.NotFound("Published practice set is unavailable.");

        var order = await _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => item.PracticeSetId == practiceSetId)
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.QuestionId)
            .ToListAsync(cancellationToken);

        var questions = await _context.ToeicQuestions
            .AsNoTracking()
            .Where(question => order.Contains(question.Id))
            .OrderBy(question => question.Id)
            .ToListAsync(cancellationToken);

        return new AttemptSourceContent(null, set, questions, await PassagesAsync(questions, cancellationToken), order);
    }

    /// <summary>Distinct media URLs of the given questions and the passages they belong to.</summary>
    public static IReadOnlyList<string> MediaReferences(AttemptSourceContent source, IReadOnlyCollection<Guid> questionIds)
    {
        var questions = source.Questions.Where(question => questionIds.Contains(question.Id)).ToList();
        var passageIds = questions.Where(question => question.PassageId.HasValue)
            .Select(question => question.PassageId!.Value)
            .ToHashSet();

        return questions
            .SelectMany(question => MediaUrls.Split(question.AudioUrl).Concat(MediaUrls.Split(question.ImageUrl)))
            .Concat(source.Passages
                .Where(passage => passageIds.Contains(passage.Id))
                .SelectMany(passage => MediaUrls.Split(passage.AudioUrl).Concat(MediaUrls.Split(passage.ImageUrl))))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>True when the content (questions, passages, order and source metadata) is unchanged.</summary>
    public static bool SameContent(AttemptSourceContent before, AttemptSourceContent current)
        => JsonSerializer.Serialize(before) == JsonSerializer.Serialize(current);

    private async Task<IReadOnlyList<ToeicPassage>> PassagesAsync(
        IReadOnlyCollection<ToeicQuestion> questions,
        CancellationToken cancellationToken)
    {
        var passageIds = questions
            .Where(question => question.PassageId.HasValue)
            .Select(question => question.PassageId!.Value)
            .Distinct()
            .ToArray();

        return await _context.ToeicPassages
            .AsNoTracking()
            .Where(passage => passageIds.Contains(passage.Id))
            .OrderBy(passage => passage.Id)
            .ToListAsync(cancellationToken);
    }
}

using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Bank.Queries.GetBankOverview;

public sealed class GetBankOverviewHandler : IRequestHandler<GetBankOverviewQuery, BankOverviewDto>
{
    private readonly IAssessmentDbContext _context;

    public GetBankOverviewHandler(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task<BankOverviewDto> Handle(
        GetBankOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var questionsByPart = await _context.ToeicQuestions
            .AsNoTracking()
            .GroupBy(question => question.Part)
            .Select(group => new
            {
                Part = group.Key,
                Questions = group.Count(),
                TestQuestions = group.Count(question => question.TestId != null)
            })
            .ToListAsync(cancellationToken);

        var passagesByPart = await _context.ToeicPassages
            .AsNoTracking()
            .GroupBy(passage => passage.Part)
            .Select(group => new { Part = group.Key, Passages = group.Count() })
            .ToDictionaryAsync(row => row.Part, row => row.Passages, cancellationToken);

        var statuses = await _context.ToeicQuestions
            .AsNoTracking()
            .GroupBy(question => question.Status)
            .Select(group => new BankStatusStatsDto(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var difficulties = await _context.ToeicQuestions
            .AsNoTracking()
            .GroupBy(question => question.DifficultyLevel)
            .Select(group => new BankDifficultyStatsDto(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var practiceSets = await _context.ToeicPracticeSets
            .AsNoTracking()
            .GroupBy(practiceSet => practiceSet.Kind)
            .Select(group => new { Kind = group.Key, Sets = group.Count() })
            .ToListAsync(cancellationToken);

        var practiceSetQuestions = await _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Join(_context.ToeicPracticeSets, item => item.PracticeSetId, practiceSet => practiceSet.Id,
                (item, practiceSet) => practiceSet.Kind)
            .GroupBy(kind => kind)
            .Select(group => new { Kind = group.Key, Questions = group.Count() })
            .ToDictionaryAsync(row => row.Kind, row => row.Questions, cancellationToken);

        var tests = await _context.ToeicTests
            .AsNoTracking()
            .GroupBy(test => test.Status)
            .Select(group => new { Status = group.Key, Tests = group.Count() })
            .ToListAsync(cancellationToken);

        var parts = Enum.GetValues<ToeicPart>()
            .Select(part =>
            {
                var counts = questionsByPart.FirstOrDefault(row => row.Part == part);
                return new BankPartStatsDto(
                    part,
                    ToeicPartRules.GetSection(part),
                    counts?.Questions ?? 0,
                    counts?.TestQuestions ?? 0,
                    (counts?.Questions ?? 0) - (counts?.TestQuestions ?? 0),
                    passagesByPart.GetValueOrDefault(part),
                    ToeicPartRules.GetStandardQuestionCount(part));
            })
            .ToList();

        return new BankOverviewDto(
            TotalQuestions: parts.Sum(part => part.Questions),
            ListeningQuestions: parts.Where(part => part.Section == ToeicSection.Listening).Sum(part => part.Questions),
            ReadingQuestions: parts.Where(part => part.Section == ToeicSection.Reading).Sum(part => part.Questions),
            TestQuestions: parts.Sum(part => part.TestQuestions),
            BankQuestions: parts.Sum(part => part.BankQuestions),
            TotalTests: tests.Sum(row => row.Tests),
            ActiveTests: tests.Where(row => row.Status == ContentStatus.Active).Sum(row => row.Tests),
            TotalPracticeSets: practiceSets.Sum(row => row.Sets),
            TotalPassages: passagesByPart.Values.Sum(),
            Parts: parts,
            Statuses: [.. statuses.OrderBy(status => status.Status)],
            Difficulties: [.. difficulties.OrderBy(difficulty => difficulty.DifficultyLevel)],
            PracticeSets: [.. practiceSets
                .Select(row => new BankPracticeSetStatsDto(row.Kind, row.Sets, practiceSetQuestions.GetValueOrDefault(row.Kind)))
                .OrderBy(row => row.Kind)]);
    }
}

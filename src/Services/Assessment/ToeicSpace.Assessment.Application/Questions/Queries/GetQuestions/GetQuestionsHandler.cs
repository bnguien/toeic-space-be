using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Questions.Queries.GetQuestions;

public sealed class GetQuestionsHandler : IRequestHandler<GetQuestionsQuery, PaginatedResult<QuestionDto>>
{
    private readonly IAssessmentDbContext _context;
    private readonly IQuestionSearch _questionSearch;

    public GetQuestionsHandler(
        IAssessmentDbContext context,
        IQuestionSearch questionSearch)
    {
        _context = context;
        _questionSearch = questionSearch;
    }

    public async Task<PaginatedResult<QuestionDto>> Handle(
        GetQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ToeicQuestions.AsNoTracking();

        if (request.TestId.HasValue)
        {
            query = query.Where(question => question.TestId == request.TestId);
        }

        if (request.PassageId.HasValue)
        {
            query = query.Where(question => question.PassageId == request.PassageId);
        }

        if (request.BankOnly.HasValue)
        {
            query = request.BankOnly.Value
                ? query.Where(question => question.TestId == null)
                : query.Where(question => question.TestId != null);
        }

        if (request.Part.HasValue)
        {
            query = query.Where(question => question.Part == request.Part);
        }

        if (request.Section.HasValue)
        {
            query = query.Where(question => question.Section == request.Section);
        }

        if (request.DifficultyLevel.HasValue)
        {
            query = query.Where(question => question.DifficultyLevel == request.DifficultyLevel);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(question => question.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = _questionSearch.Apply(query, request.Search.Trim());
        }

        if (request.PracticeSetId.HasValue)
        {
            var practiceSetId = request.PracticeSetId.Value;
            var itemQuery = _context.ToeicPracticeSetItems
                .AsNoTracking()
                .Where(item => item.PracticeSetId == practiceSetId);

            var orderedQuery = query
                .Join(itemQuery, question => question.Id, item => item.QuestionId, (question, item) => new { question, item.OrderIndex })
                .OrderBy(row => row.OrderIndex)
                .ThenBy(row => row.question.Id)
                .Select(row => row.question);

            return await ToPageAsync(orderedQuery, request, cancellationToken);
        }

        var ordered = request.TestId.HasValue
            ? query.OrderBy(question => question.QuestionNumber).ThenBy(question => question.Id)
            : query.OrderBy(question => question.Part).ThenBy(question => question.CreatedAt).ThenBy(question => question.Id);

        return await ToPageAsync(ordered, request, cancellationToken);
    }

    private static async Task<PaginatedResult<QuestionDto>> ToPageAsync(
        IQueryable<ToeicQuestion> orderedQuery,
        GetQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(QuestionMappingExtensions.DtoProjection)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<QuestionDto>(items, totalCount, request.Page, request.PageSize);
    }
}

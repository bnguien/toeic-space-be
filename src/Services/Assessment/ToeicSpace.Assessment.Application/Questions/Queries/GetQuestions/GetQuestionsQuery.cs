using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Questions.Queries.GetQuestions;

/// <param name="BankOnly">True: only questions not owned by a test. False: only test questions. Null: both.</param>
public sealed record GetQuestionsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? TestId = null,
    Guid? PassageId = null,
    Guid? PracticeSetId = null,
    bool? BankOnly = null,
    ToeicPart? Part = null,
    ToeicSection? Section = null,
    QuestionDifficulty? DifficultyLevel = null,
    ContentStatus? Status = null,
    string? Search = null) : IRequest<PaginatedResult<QuestionDto>>;

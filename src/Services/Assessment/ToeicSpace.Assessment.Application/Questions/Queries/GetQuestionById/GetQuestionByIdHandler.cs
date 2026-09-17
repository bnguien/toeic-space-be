using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;

namespace ToeicSpace.Assessment.Application.Questions.Queries.GetQuestionById;

public sealed class GetQuestionByIdHandler : IRequestHandler<GetQuestionByIdQuery, QuestionDetailDto>
{
    private readonly IAssessmentDbContext _context;

    public GetQuestionByIdHandler(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task<QuestionDetailDto> Handle(
        GetQuestionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var question = await _context.ToeicQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicQuestion), request.Id);

        var practiceSetIds = await _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => item.QuestionId == question.Id)
            .Select(item => item.PracticeSetId)
            .ToListAsync(cancellationToken);

        return question.ToDetailDto(practiceSetIds);
    }
}

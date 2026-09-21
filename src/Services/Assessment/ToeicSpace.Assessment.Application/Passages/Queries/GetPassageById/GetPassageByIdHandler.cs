using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;

namespace ToeicSpace.Assessment.Application.Passages.Queries.GetPassageById;

public sealed class GetPassageByIdHandler : IRequestHandler<GetPassageByIdQuery, PassageDetailDto>
{
    private readonly IAssessmentDbContext _context;

    public GetPassageByIdHandler(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task<PassageDetailDto> Handle(
        GetPassageByIdQuery request,
        CancellationToken cancellationToken)
    {
        var passage = await _context.ToeicPassages
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPassage), request.Id);

        var questions = await _context.ToeicQuestions
            .AsNoTracking()
            .Where(question => question.PassageId == passage.Id)
            .OrderBy(question => question.QuestionNumber)
            .ThenBy(question => question.OrderIndex)
            .ThenBy(question => question.Id)
            .Select(QuestionMappingExtensions.DtoProjection)
            .ToListAsync(cancellationToken);

        return passage.ToDetailDto(questions);
    }
}

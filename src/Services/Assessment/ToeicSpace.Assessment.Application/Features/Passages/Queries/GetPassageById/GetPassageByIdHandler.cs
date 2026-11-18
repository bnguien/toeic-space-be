using ToeicSpace.Assessment.Application.DTOs.Passages;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Mappings;


namespace ToeicSpace.Assessment.Application.Features.Passages.Queries.GetPassageById;

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

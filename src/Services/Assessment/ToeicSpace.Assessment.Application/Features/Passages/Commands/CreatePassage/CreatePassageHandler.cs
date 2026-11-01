using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.DTOs.Passages;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Mappings;


namespace ToeicSpace.Assessment.Application.Features.Passages.Commands.CreatePassage;

public sealed class CreatePassageHandler : IRequestHandler<CreatePassageCommand, PassageDto>
{
    private readonly IAssessmentDbContext _context;

    public CreatePassageHandler(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task<PassageDto> Handle(
        CreatePassageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TestId is { } testId
            && !await _context.ToeicTests.AnyAsync(test => test.Id == testId, cancellationToken))
        {
            throw AppException.NotFound(nameof(ToeicTest), testId);
        }

        var passage = new ToeicPassage
        {
            Id = Guid.NewGuid()
        };

        PassageContentValidator.ApplyContent(passage, request);

        _context.ToeicPassages.Add(passage);
        await _context.SaveChangesAsync(cancellationToken);

        return await _context.ToeicPassages
            .AsNoTracking()
            .Where(item => item.Id == passage.Id)
            .Select(PassageMappingExtensions.DtoProjection)
            .FirstAsync(cancellationToken);
    }
}

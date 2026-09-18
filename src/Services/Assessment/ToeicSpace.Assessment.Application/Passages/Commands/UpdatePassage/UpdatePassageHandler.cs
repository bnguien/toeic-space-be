using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Passages.Common;

namespace ToeicSpace.Assessment.Application.Passages.Commands.UpdatePassage;

public sealed class UpdatePassageHandler : IRequestHandler<UpdatePassageCommand, PassageDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ContentCacheInvalidator _cacheInvalidator;

    public UpdatePassageHandler(
        IAssessmentDbContext context,
        ContentCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PassageDto> Handle(
        UpdatePassageCommand request,
        CancellationToken cancellationToken)
    {
        var passage = await _context.ToeicPassages
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPassage), request.Id);

        var questionIds = await _context.ToeicQuestions
            .Where(question => question.PassageId == passage.Id)
            .Select(question => question.Id)
            .ToListAsync(cancellationToken);

        if (questionIds.Count > 0 && (passage.TestId != request.TestId || passage.Part != request.Part))
        {
            throw AppException.Conflict(
                "Cannot change the test or part of a passage that has questions. Move or delete its questions first.",
                ErrorCodes.PassageInUse);
        }

        if (request.TestId is { } testId
            && !await _context.ToeicTests.AnyAsync(test => test.Id == testId, cancellationToken))
        {
            throw AppException.NotFound(nameof(ToeicTest), testId);
        }

        PassageContentValidator.ApplyContent(passage, request);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateQuestionsAsync(questionIds, [passage.TestId], cancellationToken);

        return await _context.ToeicPassages
            .AsNoTracking()
            .Where(item => item.Id == passage.Id)
            .Select(PassageMappingExtensions.DtoProjection)
            .FirstAsync(cancellationToken);
    }
}

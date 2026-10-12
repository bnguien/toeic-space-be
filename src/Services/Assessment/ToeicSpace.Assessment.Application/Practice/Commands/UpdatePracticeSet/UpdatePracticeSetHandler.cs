using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSet;

public sealed class UpdatePracticeSetHandler : IRequestHandler<UpdatePracticeSetCommand, PracticeSetDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ContentCacheInvalidator _cacheInvalidator;

    public UpdatePracticeSetHandler(
        IAssessmentDbContext context,
        ContentCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PracticeSetDetailDto> Handle(
        UpdatePracticeSetCommand request,
        CancellationToken cancellationToken)
    {
        var practiceSet = await _context.ToeicPracticeSets
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

        var code = request.Code.Trim();

        if (!string.Equals(practiceSet.Code, code, StringComparison.Ordinal))
        {
            await _context.EnsureCodeIsAvailableAsync(code, practiceSet.Id, cancellationToken);
        }

        if (practiceSet.Part != request.Part
            && await _context.ToeicPracticeSetItems.AnyAsync(item => item.PracticeSetId == practiceSet.Id, cancellationToken))
        {
            throw AppException.Conflict(
                "Cannot change the part of a practice set that already has questions.",
                ErrorCodes.Conflict);
        }

        PracticeSetContentValidator.ApplyContent(practiceSet, request);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidatePracticeSetsAsync([practiceSet.Id], cancellationToken);

        var items = await _context.LoadItemsAsync(practiceSet.Id, cancellationToken);

        return practiceSet.ToDetailDto(items);
    }
}

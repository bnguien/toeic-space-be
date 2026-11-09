using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Common.Querying;
using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.DTOs.PracticeSets;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Mappings;
using ToeicSpace.Assessment.Application.Services;

namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.UpdatePracticeSet;

public sealed class UpdatePracticeSetHandler : IRequestHandler<UpdatePracticeSetCommand, PracticeSetDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ContentCacheInvalidator _cacheInvalidator;

    public UpdatePracticeSetHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser,
        ContentCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PracticeSetDetailDto> Handle(
        UpdatePracticeSetCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _context.ExecutePracticeSetUpdateAsync(request.Id, async () =>
        {
            var practiceSet = await _context.ToeicPracticeSets
                .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

            PracticeSetAuthorization.EnsureCanManage(practiceSet, _currentUser);
            // Compare under the set write lock; MySQL datetime(6) stores microseconds.
            if (request.ExpectedUpdatedAt is { } expected
                && expected.Ticks / 10 != (practiceSet.UpdatedAt ?? practiceSet.CreatedAt).Ticks / 10)
            {
                throw AppException.Conflict("Practice set changed. Reload before saving.", ErrorCodes.ConcurrencyConflict);
            }

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

            var items = await _context.LoadItemsAsync(practiceSet.Id, cancellationToken);

            return practiceSet.ToDetailDto(items);
        }, cancellationToken);

        await _cacheInvalidator.InvalidatePracticeSetsAsync([request.Id], cancellationToken);

        return result;
    }
}

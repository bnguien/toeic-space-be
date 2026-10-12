using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Tests.Common;

namespace ToeicSpace.Assessment.Application.Tests.Commands.UpdateTest;

public sealed class UpdateTestHandler : IRequestHandler<UpdateTestCommand, TestDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ContentCacheInvalidator _cacheInvalidator;

    public UpdateTestHandler(
        IAssessmentDbContext context,
        ContentCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<TestDetailDto> Handle(
        UpdateTestCommand request,
        CancellationToken cancellationToken)
    {
        var test = await _context.ToeicTests
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicTest), request.Id);

        var code = request.Code.Trim();

        if (!string.Equals(test.Code, code, StringComparison.Ordinal))
        {
            var codeTaken = await _context.ToeicTests
                .IgnoreQueryFilters()
                .AnyAsync(item => item.Code == code && item.Id != test.Id, cancellationToken);

            if (codeTaken)
            {
                throw AppException.Conflict($"Test code '{code}' is already used.", ErrorCodes.DuplicateCode);
            }
        }

        TestContentValidator.ApplyContent(test, request);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateTestsAsync([test.Id], cancellationToken);

        var (passageCount, questionCounts) = await _context.LoadStatisticsAsync(test.Id, cancellationToken);

        return test.ToDetailDto(passageCount, questionCounts);
    }
}

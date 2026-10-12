using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Tests.Common;

namespace ToeicSpace.Assessment.Application.Tests.Commands.UpdateTestStatus;

public sealed class UpdateTestStatusHandler : IRequestHandler<UpdateTestStatusCommand, TestDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly TestStructureInspector _structureInspector;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly ILogger<UpdateTestStatusHandler> _logger;

    public UpdateTestStatusHandler(
        IAssessmentDbContext context,
        TestStructureInspector structureInspector,
        ContentCacheInvalidator cacheInvalidator,
        ILogger<UpdateTestStatusHandler> logger)
    {
        _context = context;
        _structureInspector = structureInspector;
        _cacheInvalidator = cacheInvalidator;
        _logger = logger;
    }

    public async Task<TestDetailDto> Handle(
        UpdateTestStatusCommand request,
        CancellationToken cancellationToken)
    {
        var test = await _context.ToeicTests
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicTest), request.Id);

        if (request.Status == ContentStatus.Active && test.Status != ContentStatus.Active)
        {
            var problems = await _structureInspector.FindProblemsAsync(test.Id, cancellationToken);

            if (problems.Count > 0)
            {
                throw AppException.Conflict(
                    "The test cannot be published: " + string.Join(" ", problems),
                    ErrorCodes.TestNotPublishable);
            }
        }

        test.Status = request.Status;
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateTestsAsync([test.Id], cancellationToken);

        _logger.LogInformation(
            "Changed status of test {TestId} to {Status}",
            test.Id,
            test.Status);

        var (passageCount, questionCounts) = await _context.LoadStatisticsAsync(test.Id, cancellationToken);

        return test.ToDetailDto(passageCount, questionCounts);
    }
}

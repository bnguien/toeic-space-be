using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Tests.Common;

namespace ToeicSpace.Assessment.Application.Tests.Commands.CreateTest;

public sealed class CreateTestHandler : IRequestHandler<CreateTestCommand, TestDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateTestHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TestDetailDto> Handle(
        CreateTestCommand request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        // Soft-deleted tests keep their code (unique index), so the check ignores the soft-delete filter.
        var codeTaken = await _context.ToeicTests
            .IgnoreQueryFilters()
            .AnyAsync(test => test.Code == code, cancellationToken);

        if (codeTaken)
        {
            throw AppException.Conflict($"Test code '{code}' is already used.", ErrorCodes.DuplicateCode);
        }

        var test = new ToeicTest
        {
            Id = Guid.NewGuid(),
            Status = ContentStatus.Draft,
            CreatedByUserId = _currentUser.UserId
        };

        TestContentValidator.ApplyContent(test, request);

        _context.ToeicTests.Add(test);
        await _context.SaveChangesAsync(cancellationToken);

        return test.ToDetailDto(0, new Dictionary<ToeicPart, int>());
    }
}

using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptReview;

public sealed class GetAttemptReviewHandler : IRequestHandler<GetAttemptReviewQuery, AttemptDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptDocumentReader _documents;

    public GetAttemptReviewHandler(IAssessmentDbContext context, AttemptAccess access, AttemptDocumentReader documents)
    {
        _context = context;
        _access = access;
        _documents = documents;
    }

    public Task<AttemptDto> Handle(GetAttemptReviewQuery request, CancellationToken cancellationToken)
        => _context.ExecuteAttemptReadAsync(async () =>
        {
            var attempt = await _access.LoadOwnedAsync(request.AttemptId, track: false, cancellationToken);

            if (attempt.Status != AttemptStatus.Completed)
            {
                throw AttemptAccess.NotCompleted();
            }

            return await _documents.ReadAsync(attempt, cancellationToken);
        }, cancellationToken);
}

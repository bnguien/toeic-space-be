using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempt;

public sealed class GetAttemptHandler : IRequestHandler<GetAttemptQuery, AttemptDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptDocumentReader _documents;

    public GetAttemptHandler(IAssessmentDbContext context, AttemptAccess access, AttemptDocumentReader documents)
    {
        _context = context;
        _access = access;
        _documents = documents;
    }

    // MVCC gives Get a complete committed snapshot even if Discard commits between its queries.
    public Task<AttemptDto> Handle(GetAttemptQuery request, CancellationToken cancellationToken)
        => _context.ExecuteAttemptReadAsync(async () =>
        {
            var attempt = await _access.LoadOwnedAsync(request.AttemptId, track: false, cancellationToken);

            if (_access.IsExpired(attempt))
            {
                throw AttemptAccess.Unavailable();
            }

            return await _documents.ReadAsync(attempt, cancellationToken);
        }, cancellationToken);
}

using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptResult;

public sealed class GetAttemptResultHandler : IRequestHandler<GetAttemptResultQuery, AttemptResultDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptContentReader _contentReader;

    public GetAttemptResultHandler(IAssessmentDbContext context, AttemptAccess access, AttemptContentReader contentReader)
    {
        _context = context;
        _access = access;
        _contentReader = contentReader;
    }

    public Task<AttemptResultDto> Handle(GetAttemptResultQuery request, CancellationToken cancellationToken)
        => _context.ExecuteAttemptReadAsync(async () =>
        {
            var attempt = await _access.LoadOwnedAsync(request.AttemptId, track: false, cancellationToken);

            if (attempt.Status != AttemptStatus.Completed)
            {
                throw AttemptAccess.NotCompleted();
            }

            var content = await _contentReader.LoadAsync(attempt, trackAnswers: false, cancellationToken);
            return AttemptResultFactory.Create(content);
        }, cancellationToken);
}

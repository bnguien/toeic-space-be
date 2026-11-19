using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.ResumeAttempt;

public sealed class ResumeAttemptHandler : IRequestHandler<ResumeAttemptCommand, AttemptDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptDocumentReader _documents;

    public ResumeAttemptHandler(IAssessmentDbContext context, AttemptAccess access, AttemptDocumentReader documents)
    {
        _context = context;
        _access = access;
        _documents = documents;
    }

    public Task<AttemptDto> Handle(ResumeAttemptCommand request, CancellationToken cancellationToken)
        => _access.MutateAsync(request.AttemptId, async (attempt, _) =>
        {
            if (attempt.Status != AttemptStatus.InProgress)
            {
                throw AttemptAccess.NotActive();
            }

            attempt.LastActivityAt = _access.Now;
            await _context.SaveChangesAsync(cancellationToken);

            return await _documents.ReadAsync(attempt, cancellationToken);
        }, cancellationToken);
}

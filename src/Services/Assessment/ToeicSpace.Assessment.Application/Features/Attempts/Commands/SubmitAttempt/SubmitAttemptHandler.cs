using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SubmitAttempt;

public sealed class SubmitAttemptHandler : IRequestHandler<SubmitAttemptCommand, AttemptResultDto>
{
    private readonly AttemptAccess _access;
    private readonly AttemptContentReader _contentReader;
    private readonly AttemptGrader _grader;

    public SubmitAttemptHandler(AttemptAccess access, AttemptContentReader contentReader, AttemptGrader grader)
    {
        _access = access;
        _contentReader = contentReader;
        _grader = grader;
    }

    public Task<AttemptResultDto> Handle(SubmitAttemptCommand request, CancellationToken cancellationToken)
        => _access.MutateAsync(request.AttemptId, async (attempt, _) =>
        {
            if (attempt.Status == AttemptStatus.Abandoned)
            {
                throw AttemptAccess.NotActive();
            }

            var inProgress = attempt.Status == AttemptStatus.InProgress;
            var content = await _contentReader.LoadAsync(attempt, trackAnswers: inProgress, cancellationToken);

            if (inProgress)
            {
                await _grader.SubmitAsync(content, _access.Now, cancellationToken);
            }

            return AttemptResultFactory.Create(content);
        }, cancellationToken);
}

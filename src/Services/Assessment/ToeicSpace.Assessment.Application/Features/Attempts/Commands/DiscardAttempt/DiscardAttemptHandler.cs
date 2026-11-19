using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.DiscardAttempt;

public sealed class DiscardAttemptHandler : IRequestHandler<DiscardAttemptCommand>
{
    private readonly AttemptAccess _access;

    public DiscardAttemptHandler(AttemptAccess access)
    {
        _access = access;
    }

    public async Task Handle(DiscardAttemptCommand request, CancellationToken cancellationToken)
        => await _access.MutateAsync(request.AttemptId, async (attempt, state) =>
        {
            if (attempt.Status != AttemptStatus.InProgress)
            {
                throw AttemptAccess.NotActive();
            }

            await _access.DeleteAsync(attempt, state, cancellationToken);
            return true;
        }, cancellationToken);
}

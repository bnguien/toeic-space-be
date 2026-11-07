using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitStageAttempt;

public sealed class SubmitStageAttemptHandler(GrammarContentProvider content, GrammarProgressService progress,
    IGrammarLearningRepository repository, GrammarResultRecorder recorder, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<SubmitStageAttemptHandler> logger) : IRequestHandler<SubmitStageAttemptCommand, AttemptResultDto>
{
    public async Task<AttemptResultDto> Handle(SubmitStageAttemptCommand request, CancellationToken ct)
    {
        var stage = await content.GetStageAsync(request.Stage, ct);
        var status = await progress.StageStatusAsync(request.LearnerId, stage, ct);
        if (!status.Unlocked || request.LearnerId is not { } learnerId) throw GrammarErrors.StageLocked();
        var grade = GrammarAnswerGrading.GradeBlock(stage.Block, request.Answers);
        var now = clock.GetUtcNow().UtcDateTime;
        var attempt = new GrammarAttempt
        {
            LearnerId = learnerId, Stage = stage.Stage, Block = stage.Block.Key,
            Answers = GrammarAnswerGrading.Serialize(request.Answers), Results = grade.Correctness, Score = grade.Score,
            Total = stage.Block.Total, CreatedAt = now
        };
        repository.AddAttempt(attempt);
        await recorder.RecordAsync(learnerId, grade.Answered, now, ct);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Grammar stage attempt {AttemptId} learner {LearnerId} stage {Stage} score {Score}/{Total}",
            attempt.Id, learnerId, stage.Stage, grade.Score, stage.Block.Total);
        var after = await progress.StageStatusAsync(learnerId, stage, ct);
        return new AttemptResultDto(attempt.Id.ToString(), grade.Results, grade.Score, stage.Block.Total,
            stage.Block.PassMark, after.Best, true, null, after);
    }
}

using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitTopicAttempt;

public sealed class SubmitTopicAttemptHandler(GrammarContentProvider content, IGrammarLearningRepository repository,
    GrammarResultRecorder recorder, IGuestAttemptTokens tokens, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<SubmitTopicAttemptHandler> logger) : IRequestHandler<SubmitTopicAttemptCommand, AttemptResultDto>
{
    public async Task<AttemptResultDto> Handle(SubmitTopicAttemptCommand request, CancellationToken ct)
    {
        var topic = await content.GetTopicAsync(request.Topic, ct);
        var block = topic.Blocks.GetValueOrDefault(request.Block)
            ?? throw GrammarErrors.Invalid($"Topic {request.Topic} has no block {request.Block}.");
        var grade = GrammarAnswerGrading.GradeBlock(block, request.Answers);
        if (request.LearnerId is not { } learnerId)
            return new AttemptResultDto(tokens.Issue(new GuestAttemptRef(topic.Topic, null, block.Key)), grade.Results,
                grade.Score, block.Total, block.PassMark, null, false, null, null);

        var now = clock.GetUtcNow().UtcDateTime;
        var progress = await repository.GetLessonProgressAsync(learnerId, topic.Topic, ct);
        if (progress is null)
        {
            progress = new GrammarLessonProgress { LearnerId = learnerId, Topic = topic.Topic };
            repository.AddLessonProgress(progress);
        }
        progress.AttemptCount++;
        int best;
        if (block.Key == GrammarBlocks.MiniTest)
        {
            best = Math.Max(progress.MiniTestBest ?? grade.Score, grade.Score);
            progress.MiniTestBest = best;
        }
        else
        {
            var unit = block.Key[GrammarBlocks.QuickCheckPrefix.Length..];
            best = Math.Max(progress.QuickCheckBest.GetValueOrDefault(unit), grade.Score);
            progress.QuickCheckBest = new Dictionary<string, int>(progress.QuickCheckBest) { [unit] = best };
        }
        GrammarProgressService.MarkCompletedIfDone(topic, progress, now);
        progress.Touch(now);
        var attempt = new GrammarAttempt
        {
            LearnerId = learnerId, Topic = topic.Topic, Block = block.Key, Answers = GrammarAnswerGrading.Serialize(request.Answers),
            Results = grade.Correctness, Score = grade.Score, Total = block.Total, CreatedAt = now
        };
        repository.AddAttempt(attempt);
        await recorder.RecordAsync(learnerId, grade.Answered, now, ct);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Grammar attempt {AttemptId} learner {LearnerId} topic {Topic} block {Block} score {Score}/{Total}",
            attempt.Id, learnerId, topic.Topic, block.Key, grade.Score, block.Total);
        return new AttemptResultDto(attempt.Id.ToString(), grade.Results, grade.Score, block.Total, block.PassMark, best,
            true, GrammarProgressService.LessonDto(topic, progress), null);
    }
}

using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Commands.CheckDictationAnswer;

/// <summary>
/// Grades a submission and, for a signed-in learner, records the best accuracy / score, the pass and the clean
/// pass. A pass needs every word correct; from level 3 only a clean pass unlocks the next sentence.
/// </summary>
public sealed class CheckDictationAnswerHandler(IDictationCatalogRepository catalog,
    IDictationProgressRepository progressRepository, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<CheckDictationAnswerCommand, DictationCheckDto>
{
    public async Task<DictationCheckDto> Handle(CheckDictationAnswerCommand request, CancellationToken ct)
    {
        var item = await catalog.GetItemAsync(request.ItemId, ct) ?? throw DictationErrors.ItemNotFound();
        var userText = DictationGrader.CleanUserText(request.UserText);
        var hintCount = Math.Clamp(request.HintCount, 0, DictationRules.MaxHintCount);
        var assisted = request.Assisted || hintCount > 0;
        var result = DictationGrader.Grade(userText, item.Text);
        var score = DictationRules.Score(result.Passed, result.Accuracy);

        var bestAccuracy = result.Accuracy;
        bool saved = false, passedClean = false, unlocksNext = false, nextUnlocked = false;
        if (request.LearnerId is { } learner && (result.Passed || !request.DryRun))
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var progress = await progressRepository.GetForUpdateAsync(learner, item.Id, ct);
            if (progress is null)
            {
                progress = new DictationProgress { LearnerId = learner, ItemId = item.Id, CreatedAt = now };
                progressRepository.Add(progress);
            }
            progress.Record(result.Accuracy, score, result.Passed, assisted, now);
            await unitOfWork.SaveChangesAsync(ct);
            bestAccuracy = progress.BestAccuracy;
            passedClean = progress.PassedClean;
            saved = true;
            unlocksNext = DictationRules.PassesUnlock(item.Level, progress.Passed, progress.PassedClean);
            if (unlocksNext) nextUnlocked = (await catalog.GetFollowingItemsAsync(item, 1, ct)).Count > 0;
        }

        // A dry run only needs the word statuses: the text of wrong/missing words would reveal the answer.
        var reveal = !request.DryRun || result.Passed;
        var words = result.Words.Select(w => reveal
            ? w.ToDto()
            : new CheckWordDto(w.Status is DictationWordStatus.Correct or DictationWordStatus.Extra ? w.Text : string.Empty,
                w.Status, w.Typed ?? string.Empty)).ToArray();
        return new DictationCheckDto(result.Accuracy, result.Passed, result.CorrectCount, result.TotalCount, words,
            reveal ? item.Text : string.Empty, reveal ? item.Translation : string.Empty, request.DryRun, score,
            Math.Round(bestAccuracy, 1), saved, assisted, passedClean, unlocksNext, nextUnlocked);
    }
}

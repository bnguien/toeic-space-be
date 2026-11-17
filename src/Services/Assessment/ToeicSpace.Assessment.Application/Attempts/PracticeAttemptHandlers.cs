using System.Text.Json;
using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Services;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Attempts;

public sealed class PracticeAttemptHandlers(IAssessmentDbContext db, ICurrentUserService user,
    IAttemptMediaStore media, TimeProvider clock, ILogger<PracticeAttemptHandlers> logger) :
    IRequestHandler<StartPracticeAttempt, PracticeAttemptDto>, IRequestHandler<GetPracticeAttempt, PracticeAttemptDto>,
    IRequestHandler<GetActivePracticeAttempt, ActivePracticeAttemptDto>, IRequestHandler<ResumePracticeAttempt, PracticeAttemptDto>,
    IRequestHandler<SavePracticeAnswer, SavedAnswerDto>, IRequestHandler<DiscardPracticeAttempt>,
    IRequestHandler<CleanupPracticeAttempts, int>
{
    public const int RetentionDays = 7;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private Guid Owner => user.IsAuthenticated && user.UserId is { } id && !user.CanManageContent
        ? id : throw AppException.Forbidden("Only authenticated students can access practice attempts.");
    private static AppException Missing() => AppException.NotFound("Practice attempt is unavailable.", "attempt_unavailable");
    private bool Expired(ToeicAttempt a) => a.LastActivityAt <= Now.AddDays(-RetentionDays);

    public async Task<ActivePracticeAttemptDto> Handle(GetActivePracticeAttempt request, CancellationToken ct)
    {
        var owner = Owner;
        var generation = await db.PracticeAttemptStates.AsNoTracking().Where(s => s.UserId == owner && s.PracticeSetId == request.PracticeSetId)
            .Select(s => (long?)s.Generation).SingleOrDefaultAsync(ct) ?? 0;
        var cutoff = Now.AddDays(-RetentionDays);
        var id = await Eligible().Where(a => a.UserId == owner && a.PracticeSetId == request.PracticeSetId &&
            a.Status == AttemptStatus.InProgress && a.LastActivityAt > cutoff).Select(a => (Guid?)a.Id).SingleOrDefaultAsync(ct);
        return new(generation, id);
    }

    public async Task<PracticeAttemptDto> Handle(GetPracticeAttempt request, CancellationToken ct)
        => await db.ExecuteAttemptReadAsync(async () => {
        var a = await LoadOwned(request.Id, ct);
        if (Expired(a)) throw Missing();
        return await PublicDto(a, ct);
    }, ct);

    public async Task<PracticeAttemptDto> Handle(StartPracticeAttempt request, CancellationToken ct)
    {
        var owner = Owner;
        // Cheap retry/expired path BEFORE any external media work.
        var existing = await db.ToeicAttempts.AsNoTracking().Where(a => a.UserId == owner &&
            a.PracticeSetId == request.PracticeSetId && a.Status == AttemptStatus.InProgress).SingleOrDefaultAsync(ct);
        if (existing != null)
        {
            var resumed = await db.ExecuteAttemptUpdateAsync(owner, request.PracticeSetId, async () =>
            {
                var state = await State(owner, request.PracticeSetId, ct);
                if (state.Generation != request.ExpectedGeneration) throw StaleStart();
                var current = await Eligible().SingleOrDefaultAsync(a => a.Id == existing.Id && a.UserId == owner, ct);
                if (current == null) return null;
                if (Expired(current)) { await Delete(current, state, ct); return null; }
                return await PublicDto(current, ct); // Start retry is NOT Resume/activity.
            }, ct);
            return resumed ?? throw Missing();
        }

        // Prepare copies outside all DB locks. Revalidate the exact source inside the transaction.
        var generation = await db.PracticeAttemptStates.AsNoTracking()
            .Where(s => s.UserId == owner && s.PracticeSetId == request.PracticeSetId)
            .Select(s => (long?)s.Generation).SingleOrDefaultAsync(ct) ?? 0;
        if (generation != request.ExpectedGeneration) throw StaleStart();
        var before = await ReadSource(request.PracticeSetId, ct);
        await PracticeSetPublishingGuard.EnsureValidAsync(db, before.Set, before.Order, ct, lockContent: false);
        var prepared = await media.PreserveAsync(SourceUrls(before), ct);
        var result = await db.ExecuteAttemptUpdateAsync(owner, request.PracticeSetId, async () =>
        {
            var state = await State(owner, request.PracticeSetId, ct);
            if (state.Generation != request.ExpectedGeneration) throw StaleStart();
            var active = await Eligible().SingleOrDefaultAsync(a => a.UserId == owner && a.PracticeSetId == request.PracticeSetId && a.Status == AttemptStatus.InProgress, ct);
            if (active != null)
            {
                if (Expired(active)) { await Delete(active, state, ct); return null; }
                return await PublicDto(active, ct);
            }
            await db.LockAttemptSetAsync(request.PracticeSetId, ct);
            var ids = await db.ToeicPracticeSetItems.Where(i => i.PracticeSetId == request.PracticeSetId)
                .OrderBy(i => i.OrderIndex).Select(i => i.QuestionId).ToListAsync(ct);
            await db.LockPracticeContentAsync(ids, ct);
            var current = await ReadSource(request.PracticeSetId, ct);
            if (JsonSerializer.Serialize(current) != JsonSerializer.Serialize(before))
                throw AppException.Conflict("Practice content changed while media was prepared. Start again.", "concurrency_conflict");
            await PracticeSetPublishingGuard.EnsureValidAsync(db, current.Set, ids, ct, lockContent: false);
            var now = Now;
            var attempt = new ToeicAttempt { Id = Guid.NewGuid(), UserId = owner, PracticeSetId = request.PracticeSetId,
                Mode = AttemptMode.Practice, Status = AttemptStatus.InProgress, StartTime = now, LastActivityAt = now,
                TotalQuestions = ids.Count };
            var set = current.Set;
            attempt.Snapshot = new ToeicAttemptSnapshot { AttemptId = attempt.Id, CapturedAt = now,
                Payload = JsonSerializer.Serialize(new SnapshotDocument(
                    new(set.Id, set.Code, set.Title, set.Description, set.Part, set.Kind, set.Level, set.TargetScore, set.DurationMinutes),
                    current.Passages.Select(p => new PassageSnapshot(p.Id, p.Part, p.PassageType, p.Title, p.Content, p.Transcript,
                        Replace(p.AudioUrl, prepared), Replace(p.ImageUrl, prepared), p.OrderIndex)).ToArray())) };
            db.ToeicAttempts.Add(attempt);
            var questions = current.Questions.ToDictionary(q => q.Id);
            for (var index = 0; index < ids.Count; index++)
            {
                var q = questions[ids[index]];
                db.ToeicAttemptQuestions.Add(new ToeicAttemptQuestion { Id = Guid.NewGuid(), AttemptId = attempt.Id,
                    SourceQuestionId = q.Id, Version = q.Version, OrderIndex = index,
                    Payload = JsonSerializer.Serialize(new QuestionSnapshot(q.Id, q.PassageId, q.Part, q.Version, q.QuestionNumber,
                        q.QuestionText, q.OptionA, q.OptionB, q.OptionC, q.OptionD, Replace(q.AudioUrl, prepared),
                        Replace(q.ImageUrl, prepared), q.Transcript, q.CorrectAnswer, q.Explanation, q.DifficultyLevel, q.Topic)) });
            }
            await db.SaveChangesAsync(ct);
            return await PublicDto(attempt, ct);
        }, ct);
        return result ?? throw Missing(); // Deletion has committed before this error is raised.
    }

    public async Task<PracticeAttemptDto> Handle(ResumePracticeAttempt request, CancellationToken ct)
        => await Mutate(request.Id, async (a, _, token) =>
        {
            a.LastActivityAt = Now;
            await db.SaveChangesAsync(token);
            return await PublicDto(a, token);
        }, ct);

    public async Task<SavedAnswerDto> Handle(SavePracticeAnswer request, CancellationToken ct)
        => await Mutate(request.Id, async (a, _, token) =>
        {
            var row = await db.ToeicAttemptQuestions.SingleOrDefaultAsync(q => q.AttemptId == a.Id && q.Id == request.QuestionId, token)
                ?? throw AppException.Validation("Question does not belong to this attempt.");
            var q = JsonSerializer.Deserialize<QuestionSnapshot>(row.Payload)!;
            if (request.Answer is { } option && !ToeicPartRules.IsValidAnswer(q.Part, option))
                throw AppException.Validation("Answer option is invalid for this part.");
            var answer = await db.ToeicAttemptAnswers.SingleOrDefaultAsync(x => x.AttemptId == a.Id && x.SnapshotQuestionId == row.Id, token);
            if (answer?.LastMutationId == request.MutationId)
            {
                if (answer.UserAnswer != request.Answer) throw AppException.Conflict("Mutation ID was already used.", "mutation_conflict");
                return new SavedAnswerDto(row.Id, answer.UserAnswer, answer.Revision, Utc(a.LastActivityAt!.Value));
            }
            if ((answer?.Revision ?? 0) != request.ExpectedRevision)
                throw AppException.Conflict("Answer changed in another session. Reload before choosing which answer to keep.", "answer_revision_conflict");
            if (answer == null)
            {
                answer = new ToeicAttemptAnswer { Id = Guid.NewGuid(), AttemptId = a.Id, QuestionId = row.SourceQuestionId,
                    SnapshotQuestionId = row.Id, CorrectAnswer = q.CorrectAnswer };
                db.ToeicAttemptAnswers.Add(answer);
            }
            answer.UserAnswer = request.Answer;
            answer.Revision++;
            answer.LastMutationId = request.MutationId;
            a.LastActivityAt = Now;
            await db.SaveChangesAsync(token);
            return new SavedAnswerDto(row.Id, answer.UserAnswer, answer.Revision, Utc(a.LastActivityAt.Value));
        }, ct);

    public async Task Handle(DiscardPracticeAttempt request, CancellationToken ct)
    {
        await Mutate(request.Id, async (a, state, token) => { await Delete(a, state, token); return true; }, ct);
    }

    public async Task<int> Handle(CleanupPracticeAttempts request, CancellationToken ct)
    {
        var cutoff = Now.AddDays(-RetentionDays);
        var candidates = await Eligible().AsNoTracking().Where(a => a.Status == AttemptStatus.InProgress && a.LastActivityAt <= cutoff)
            .OrderBy(a => a.LastActivityAt).Take(100).Select(a => new { a.Id, a.UserId, SetId = a.PracticeSetId!.Value }).ToListAsync(ct);
        var deleted = 0;
        var failed = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                deleted += await db.ExecuteAttemptUpdateAsync(candidate.UserId, candidate.SetId, async () =>
                {
                    var a = await Eligible().SingleOrDefaultAsync(a => a.Id == candidate.Id, ct);
                    if (a == null || a.Status != AttemptStatus.InProgress || !Expired(a)) return 0;
                    await Delete(a, await State(candidate.UserId, candidate.SetId, ct), ct);
                    return 1;
                }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { failed++; logger.LogError("Practice cleanup failed for {AttemptId}: {ErrorType}", candidate.Id, ex.GetType().Name); }
        }
        logger.LogInformation("Practice cleanup checked {Checked}, deleted {Deleted}, skipped {Skipped}, failed {Failed} at {Utc}",
            candidates.Count, deleted, candidates.Count - deleted - failed, failed, Now);
        return deleted;
    }

    private IQueryable<ToeicAttempt> Eligible() => db.ToeicAttempts.Where(a => a.PracticeSetId != null && a.Mode == AttemptMode.Practice &&
        a.LastActivityAt != null && a.Snapshot != null && a.Snapshot.SchemaVersion == 1);

    private async Task<ToeicAttempt> LoadOwned(Guid id, CancellationToken ct)
    {
        var owner = Owner;
        var a = await db.ToeicAttempts.SingleOrDefaultAsync(a => a.Id == id && a.UserId == owner, ct) ?? throw Missing();
        if (a.PracticeSetId == null || a.LastActivityAt == null || a.Mode != AttemptMode.Practice ||
            !await db.ToeicAttemptSnapshots.AnyAsync(s => s.AttemptId == id && s.SchemaVersion == 1, ct))
            throw AppException.Conflict("Legacy attempts are not supported by this practice API.", "legacy_attempt_unsupported");
        if (a.Status != AttemptStatus.InProgress) throw AppException.Conflict("This attempt is no longer active.", "attempt_not_active");
        return a;
    }

    private async Task<T> Mutate<T>(Guid id, Func<ToeicAttempt, PracticeAttemptState, CancellationToken, Task<T>> mutate, CancellationToken ct)
    {
        var initial = await LoadOwned(id, ct);
        var owner = Owner;
        var result = await db.ExecuteAttemptUpdateAsync(owner, initial.PracticeSetId!.Value, async () =>
        {
            var a = await LoadOwned(id, ct);
            var state = await State(owner, a.PracticeSetId!.Value, ct);
            if (Expired(a)) { await Delete(a, state, ct); return (Expired: true, Value: default(T)!); }
            return (Expired: false, Value: await mutate(a, state, ct));
        }, ct);
        if (result.Expired) throw Missing();
        return result.Value;
    }

    private async Task<PracticeAttemptState> State(Guid owner, Guid setId, CancellationToken ct)
    {
        var state = await db.PracticeAttemptStates.SingleOrDefaultAsync(s => s.UserId == owner && s.PracticeSetId == setId, ct);
        if (state != null) return state;
        state = new() { UserId = owner, PracticeSetId = setId };
        db.PracticeAttemptStates.Add(state);
        await db.SaveChangesAsync(ct);
        return state;
    }

    private async Task Delete(ToeicAttempt a, PracticeAttemptState state, CancellationToken ct)
    {
        // Delete only Attempt-owned progress; shared sources and retained media are preserved.
        db.ToeicAttemptAnswers.RemoveRange(await db.ToeicAttemptAnswers.Where(x => x.AttemptId == a.Id).ToListAsync(ct));
        db.ToeicAttemptQuestions.RemoveRange(await db.ToeicAttemptQuestions.Where(x => x.AttemptId == a.Id).ToListAsync(ct));
        db.ToeicAttemptSnapshots.RemoveRange(await db.ToeicAttemptSnapshots.Where(x => x.AttemptId == a.Id).ToListAsync(ct));
        db.ToeicAttempts.Remove(a);
        state.Generation++;
        await db.SaveChangesAsync(ct);
    }

    private async Task<PracticeAttemptDto> PublicDto(ToeicAttempt a, CancellationToken ct)
    {
        var snapshot = await db.ToeicAttemptSnapshots.AsNoTracking().SingleAsync(s => s.AttemptId == a.Id, ct);
        var document = JsonSerializer.Deserialize<SnapshotDocument>(snapshot.Payload)!;
        var answers = await db.ToeicAttemptAnswers.AsNoTracking().Where(x => x.AttemptId == a.Id && x.SnapshotQuestionId != null)
            .ToDictionaryAsync(x => x.SnapshotQuestionId!.Value, ct);
        var rows = await db.ToeicAttemptQuestions.AsNoTracking().Where(x => x.AttemptId == a.Id).OrderBy(x => x.OrderIndex).ToListAsync(ct);
        var questions = rows.Select(row =>
        {
            var q = JsonSerializer.Deserialize<QuestionSnapshot>(row.Payload)!;
            answers.TryGetValue(row.Id, out var answer);
            return new AttemptQuestionDto(row.Id, q.Id, q.PassageId, q.Part, row.Version, row.OrderIndex, q.QuestionNumber,
                q.QuestionText, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.AudioUrl, q.ImageUrl,
                TranslatedText.TranscriptText(q.Transcript), answer?.UserAnswer, answer?.Revision ?? 0);
        }).ToArray();
        var passages = document.Passages.Select(p => new AttemptPassageDto(p.Id, p.Part, p.Title,
            TranslatedText.PassageText(p.Content), TranslatedText.TranscriptText(p.Transcript), p.AudioUrl, p.ImageUrl)).ToArray();
        return new(a.Id, a.PracticeSetId!.Value, a.Status.ToString(), Utc(a.StartTime), Utc(a.LastActivityAt!.Value),
            Utc(a.LastActivityAt.Value.AddDays(RetentionDays)), document.Set, questions, passages);
    }

    private sealed record Source(ToeicPracticeSet Set, IReadOnlyList<ToeicQuestion> Questions, IReadOnlyList<ToeicPassage> Passages, IReadOnlyList<Guid> Order);
    private async Task<Source> ReadSource(Guid setId, CancellationToken ct)
    {
        var set = await db.ToeicPracticeSets.AsNoTracking().SingleOrDefaultAsync(s => s.Id == setId && s.Status == ContentStatus.Active, ct)
            ?? throw AppException.NotFound("Published practice set is unavailable.");
        var ids = await db.ToeicPracticeSetItems.AsNoTracking().Where(i => i.PracticeSetId == setId).OrderBy(i => i.OrderIndex).Select(i => i.QuestionId).ToListAsync(ct);
        var questions = await db.ToeicQuestions.AsNoTracking().Where(q => ids.Contains(q.Id)).OrderBy(q => q.Id).ToListAsync(ct);
        var passageIds = questions.Where(q => q.PassageId.HasValue).Select(q => q.PassageId!.Value).Distinct().ToArray();
        var passages = await db.ToeicPassages.AsNoTracking().Where(p => passageIds.Contains(p.Id)).OrderBy(p => p.Id).ToListAsync(ct);
        return new(set, questions, passages, ids);
    }
    private static IEnumerable<string> SourceUrls(Source source) => source.Questions.SelectMany(q => MediaUrls.Split(q.AudioUrl).Concat(MediaUrls.Split(q.ImageUrl)))
        .Concat(source.Passages.SelectMany(p => MediaUrls.Split(p.AudioUrl).Concat(MediaUrls.Split(p.ImageUrl)))).Distinct(StringComparer.Ordinal);
    private static string? Replace(string? value, IReadOnlyDictionary<string, string> preserved) => value == null ? null :
        string.Join(MediaUrls.ImageSeparator, MediaUrls.Split(value).Select(url => preserved[url]));
    private static AppException StaleStart() => AppException.Conflict("Start request has expired. Reload the practice set.", "stale_start_generation");
}

using ToeicSpace.Assessment.Application.Common.Content;

namespace ToeicSpace.Assessment.Application.Attempts;

public sealed record SetSnapshot(Guid Id, string Code, string Title, string? Description, ToeicPart Part,
    PracticeSetKind Kind, int? Level, int? TargetScore, int DurationMinutes);
public sealed record QuestionSnapshot(Guid Id, Guid? PassageId, ToeicPart Part, int Version, int? QuestionNumber,
    string? QuestionText, string OptionA, string OptionB, string OptionC, string? OptionD,
    string? AudioUrl, string? ImageUrl, string? Transcript, AnswerOption CorrectAnswer, string? Explanation,
    QuestionDifficulty Difficulty, string? Topic);
public sealed record PassageSnapshot(Guid Id, ToeicPart Part, string? PassageType, string? Title, string? Content,
    string? Transcript, string? AudioUrl, string? ImageUrl, int OrderIndex);
public sealed record SnapshotDocument(SetSnapshot Set, IReadOnlyList<PassageSnapshot> Passages);

// Deliberately distinct from bank/preview DTOs: no private fields, even when null/default.
public sealed record AttemptQuestionDto(Guid Id, Guid SourceQuestionId, Guid? PassageId, ToeicPart Part,
    int Version, int OrderIndex, int? QuestionNumber, string? QuestionText, string OptionA, string OptionB,
    string OptionC, string? OptionD, string? AudioUrl, string? ImageUrl, string? Transcript,
    AnswerOption? UserAnswer, long Revision);
public sealed record AttemptPassageDto(Guid Id, ToeicPart Part, string? Title, string? Content,
    string? Transcript, string? AudioUrl, string? ImageUrl);
public sealed record PracticeAttemptDto(Guid Id, Guid PracticeSetId, string Status, DateTime StartTime,
    DateTime LastActivityAt, DateTime ExpiresAt, SetSnapshot Set, IReadOnlyList<AttemptQuestionDto> Questions,
    IReadOnlyList<AttemptPassageDto> Passages);
public sealed record ActivePracticeAttemptDto(long Generation, Guid? AttemptId);
public sealed record SavedAnswerDto(Guid QuestionId, AnswerOption? UserAnswer, long Revision, DateTime LastActivityAt);

public interface IAttemptMediaStore
{
    // Keys are original absolute URLs; values are immutable URLs served by our gateway.
    Task<IReadOnlyDictionary<string, string>> PreserveAsync(IEnumerable<string> urls, CancellationToken ct);
    Task<(Stream Content, string ContentType)> OpenAsync(string hash, CancellationToken ct);
}

public sealed record StartPracticeAttempt(Guid PracticeSetId, long ExpectedGeneration) : IRequest<PracticeAttemptDto>;
public sealed record GetPracticeAttempt(Guid Id) : IRequest<PracticeAttemptDto>;
public sealed record GetActivePracticeAttempt(Guid PracticeSetId) : IRequest<ActivePracticeAttemptDto>;
public sealed record ResumePracticeAttempt(Guid Id) : IRequest<PracticeAttemptDto>;
public sealed record SavePracticeAnswer(Guid Id, Guid QuestionId, AnswerOption? Answer, long ExpectedRevision, Guid MutationId)
    : IRequest<SavedAnswerDto>;
public sealed record DiscardPracticeAttempt(Guid Id) : IRequest;
public sealed record CleanupPracticeAttempts : IRequest<int>;

public sealed class StartPracticeAttemptValidator : AbstractValidator<StartPracticeAttempt>
{
    public StartPracticeAttemptValidator()
    {
        RuleFor(x => x.PracticeSetId).NotEmpty();
        RuleFor(x => x.ExpectedGeneration).GreaterThanOrEqualTo(0);
    }
}
public sealed class SavePracticeAnswerValidator : AbstractValidator<SavePracticeAnswer>
{
    public SavePracticeAnswerValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.MutationId).NotEmpty();
        RuleFor(x => x.ExpectedRevision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Answer!.Value).IsInEnum().When(x => x.Answer.HasValue);
    }
}

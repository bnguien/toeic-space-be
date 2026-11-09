using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts;

/// <summary>
/// attemptId → (attempt, block). Another learner's attempt, a bad or expired guest ticket, or a guest ticket sent while
/// signed in all read as 404 <c>grammar_attempt_not_found</c>.
/// </summary>
public sealed class GrammarAttemptResolver(GrammarContentProvider content, IGrammarLearningRepository repository,
    IGuestAttemptTokens tokens)
{
    public async Task<(GrammarAttempt? Attempt, GrammarBlock Block, GrammarQuestionInfo Question)> ResolveAsync(
        Guid? learnerId, string attemptId, string questionId, CancellationToken ct)
    {
        var bundle = await content.GetAsync(ct);
        GrammarAttempt? attempt = null;
        GrammarBlock? block;
        var reference = attemptId ?? string.Empty;
        if (reference.StartsWith(IGuestAttemptTokens.Prefix, StringComparison.Ordinal))
        {
            if (learnerId is not null) throw GrammarErrors.AttemptNotFound();
            var guest = tokens.Read(reference) ?? throw GrammarErrors.AttemptNotFound();
            block = bundle.BlockOf(guest.Topic, guest.Stage, guest.Block);
        }
        else
        {
            if (learnerId is not { } id || reference.Length is 0 or > 18 || !reference.All(char.IsAsciiDigit))
                throw GrammarErrors.AttemptNotFound();
            attempt = await repository.GetAttemptAsync(id, long.Parse(reference), ct) ?? throw GrammarErrors.AttemptNotFound();
            block = bundle.BlockOf(attempt.Topic, attempt.Stage, attempt.Block);
        }
        if (block is null) throw GrammarErrors.AttemptNotFound();
        var question = bundle.Questions.GetValueOrDefault(questionId ?? string.Empty);
        if (question is null || !block.Ids.Contains(question.Id))
            throw GrammarErrors.Invalid("The question is not part of this attempt.");
        return (attempt, block, question);
    }
}

using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;

/// <summary>
/// Builds one question per word id, in the same order. Nothing is stored. <see cref="LearnerId"/> comes from the
/// access token when one is sent (Guid.Empty for guests) and unlocks the caller's own custom words.
/// </summary>
public sealed record CreateQuizCommand(IReadOnlyList<Guid> WordIds,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<IReadOnlyList<QuizQuestionDto>>
{
    public const int MaxWords = 50;
}

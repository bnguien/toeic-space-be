using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

public sealed record QuizQuestionDto(Guid WordId, string Prompt, PartOfSpeech PartOfSpeech,
    IReadOnlyList<QuizOptionDto> Options);

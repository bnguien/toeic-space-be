namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

public sealed record SetDetailDto(Guid Id, Guid StageId, string StageSlug, string StageTitle, string Slug,
    string Title, int Order, Guid? PreviousSetId, Guid? NextSetId, IReadOnlyList<WordDto> Words);

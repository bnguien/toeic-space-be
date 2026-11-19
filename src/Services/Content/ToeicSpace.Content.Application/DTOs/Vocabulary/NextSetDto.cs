namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

public sealed record NextSetDto(Guid StageId, Guid SetId, string Title, int Order);

using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.ConfirmThumbnail;

public sealed record ConfirmThumbnailCommand(string Key, Guid AdminId) : IRequest<ConfirmedThumbnailDto>;
public sealed record ConfirmThumbnailsCommand(IReadOnlyList<string> Keys, Guid AdminId)
    : IRequest<ConfirmThumbnailsResult>;

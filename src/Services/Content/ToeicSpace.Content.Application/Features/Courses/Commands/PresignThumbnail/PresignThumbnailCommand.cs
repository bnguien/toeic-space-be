using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.PresignThumbnail;

public sealed record PresignThumbnailCommand(string ContentType, long SizeBytes, Guid AdminId)
    : IRequest<PresignedThumbnailDto>;

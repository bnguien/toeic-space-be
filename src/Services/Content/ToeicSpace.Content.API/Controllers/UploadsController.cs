using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Features.Courses.Commands.ConfirmThumbnail;
using ToeicSpace.Content.Application.Features.Courses.Commands.PresignThumbnail;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

public sealed record PresignThumbnailRequest(string ContentType, long SizeBytes);
public sealed record ConfirmThumbnailRequest(string Key);
public sealed record ConfirmThumbnailsRequest(IReadOnlyList<string> Keys);

[ApiController]
[Route("api/v1/uploads")]
[Authorize(Policy = "Admin")]
public sealed class UploadsController(ISender sender) : ControllerBase
{
    private Guid AdminId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpPost("presign")]
    public Task<PresignedThumbnailDto> Presign(PresignThumbnailRequest request, CancellationToken ct) =>
        sender.Send(new PresignThumbnailCommand(request.ContentType, request.SizeBytes, AdminId), ct);

    [HttpPost("confirm")]
    public Task<ConfirmedThumbnailDto> Confirm(ConfirmThumbnailRequest request, CancellationToken ct) =>
        sender.Send(new ConfirmThumbnailCommand(request.Key, AdminId), ct);

    [HttpPost("confirm/bulk")]
    public Task<ConfirmThumbnailsResult> ConfirmBulk(ConfirmThumbnailsRequest request, CancellationToken ct) =>
        sender.Send(new ConfirmThumbnailsCommand(request.Keys, AdminId), ct);
}

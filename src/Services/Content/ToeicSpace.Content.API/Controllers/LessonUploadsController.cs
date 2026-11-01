using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Features.Lessons.Commands.ConfirmLessonUpload;
using ToeicSpace.Content.Application.Features.Lessons.Commands.PresignLessonUpload;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

public sealed record PresignLessonMediaRequest(LessonUploadKind Kind, string FileName,
    string ContentType, long SizeBytes);
public sealed record ConfirmLessonMediaRequest(string Key);
public sealed record PresignLessonMediaBatchRequest(IReadOnlyList<ToeicSpace.Content.Application.DTOs.Lessons.LessonUploadRequest> Files);
public sealed record ConfirmLessonMediaBatchRequest(IReadOnlyList<string> Keys);

[ApiController]
[Route("api/v1/uploads/lessons")]
[Authorize(Policy = "Admin")]
public sealed class LessonUploadsController(ISender sender) : ControllerBase
{
    private Guid AdminId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpPost("presign")]
    public Task<PresignedLessonUploadDto> Presign(PresignLessonMediaRequest request, CancellationToken ct) =>
        sender.Send(new PresignLessonUploadCommand(request.Kind, request.FileName,
            request.ContentType, request.SizeBytes, AdminId), ct);

    [HttpPost("presign/bulk")]
    public Task<PresignLessonUploadsResult> PresignBulk(PresignLessonMediaBatchRequest request, CancellationToken ct) =>
        sender.Send(new PresignLessonUploadsCommand(request.Files, AdminId), ct);

    [HttpPost("confirm")]
    public Task<ConfirmedLessonUploadDto> Confirm(ConfirmLessonMediaRequest request, CancellationToken ct) =>
        sender.Send(new ConfirmLessonUploadCommand(request.Key, AdminId), ct);

    [HttpPost("confirm/bulk")]
    public Task<ConfirmLessonUploadsResult> ConfirmBulk(ConfirmLessonMediaBatchRequest request, CancellationToken ct) =>
        sender.Send(new ConfirmLessonUploadsCommand(request.Keys, AdminId), ct);
}

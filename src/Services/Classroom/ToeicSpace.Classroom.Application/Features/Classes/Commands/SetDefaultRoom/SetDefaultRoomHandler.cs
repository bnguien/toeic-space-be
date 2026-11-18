using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.SetDefaultRoom;

public sealed class SetDefaultRoomHandler(IFacilityRepository facilities, IClassRepository classes, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<SetDefaultRoomHandler> logger, IClassSchedulingService? scheduling = null)
    : IRequestHandler<SetDefaultRoomCommand, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(SetDefaultRoomCommand request, CancellationToken ct)
    {
        await using var tx = request.ApplyToFutureSessions
            ? await unitOfWork.BeginSchedulingTransactionAsync(ct)
            : await unitOfWork.BeginTransactionAsync(ct);
        var entity = await facilities.LockClassAsync(request.ClassId, ct) ?? throw ClassRuleException.NotFound();
        if (!entity.CanEdit()) throw ClassRuleException.InvalidState();
        if (entity.DefaultRoomId == request.RoomId)
        {
            if (request.ApplyToFutureSessions)
            {
                if (scheduling is null) throw new InvalidOperationException("Scheduling service is required.");
                await scheduling.PrepareDefaultRoomChangeAsync(entity.Id, request.RoomId, clock.GetUtcNow().UtcDateTime, ct);
                await scheduling.ApplyDefaultRoomAsync(entity.Id, request.RoomId, request.AdminId,
                    request.Reason, clock.GetUtcNow().UtcDateTime, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return (await classes.GetDetailAsync(entity.Id, ct))!.Detail();
        }
        if (entity.DefaultRoomId is not null && string.IsNullOrWhiteSpace(request.Reason))
            throw new ClassRuleException("room_change_reason_required", "A reason is required to replace or clear a room.");
        if (request.Reason?.Trim().Length > 500)
            throw new ClassRuleException("invalid_room_reason", "Reason must be at most 500 characters.");
        if (request.ApplyToFutureSessions)
        {
            if (scheduling is null) throw new InvalidOperationException("Scheduling service is required.");
            await scheduling.PrepareDefaultRoomChangeAsync(entity.Id, request.RoomId, clock.GetUtcNow().UtcDateTime, ct);
        }
        if (request.RoomId is { } roomId)
        {
            var candidate = await facilities.RoomAsync(roomId, ct)
                ?? throw new ClassRuleException("room_not_found", "Room not found.", 404);
            var center = await facilities.LockCenterAsync(candidate.CenterId, ct)
                ?? throw new ClassRuleException("center_not_found", "Center not found.", 404);
            var room = await facilities.LockRoomAsync(roomId, ct)
                ?? throw new ClassRuleException("room_not_found", "Room not found.", 404);
            if (center.Status != FacilityStatus.Active || room.Status != FacilityStatus.Active || room.CenterId != center.Id)
                throw new ClassRuleException("inactive_room", "Room and center must be active.", 409);
            if (await facilities.ActiveEnrollmentCountAsync(entity.Id, ct) > room.Capacity)
                throw new ClassRuleException("room_too_small", "Room capacity is below active enrollment count.", 409);
        }
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.ApplyToFutureSessions)
        {
            if (scheduling is null) throw new InvalidOperationException("Scheduling service is required.");
            await scheduling.ApplyDefaultRoomAsync(entity.Id, request.RoomId, request.AdminId, request.Reason, now, ct);
        }
        facilities.Add(new ClassRoomChange { ClassId = entity.Id, PreviousRoomId = entity.DefaultRoomId,
            NewRoomId = request.RoomId, ChangedBy = request.AdminId, ChangedAt = now, Reason = request.Reason?.Trim() });
        entity.DefaultRoomId = request.RoomId;
        entity.Touch(request.AdminId, now);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Default room changed on class {ClassId} by {AdminId}; version {Version}",
            entity.Id, request.AdminId, entity.Version);
        return (await classes.GetDetailAsync(entity.Id, ct))!.Detail();
    }
}

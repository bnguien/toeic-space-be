using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.UpdateClass;

public sealed class UpdateClassHandler(
    IClassRepository db,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<UpdateClassHandler> logger) : IRequestHandler<UpdateClassCommand, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(UpdateClassCommand request, CancellationToken ct)
    {
        var id = request.ClassId;
        var adminId = request.AdminId;

        var entity = await db.GetByIdAsync(id, false, ct) ?? throw ClassRuleException.NotFound();
        if (request.HasCode && request.Code != entity.Code)
            throw new ClassRuleException("immutable_code", "Class code cannot be changed.");
        if (!entity.CanEdit())
            throw ClassRuleException.InvalidState();

        // Preserve omitted fields; explicit null is meaningful only for nullable metadata.
        var name = request.HasName
            ? request.Name ?? throw new ClassRuleException("invalid_class", "Class name is required.")
            : entity.Name;
        var description = request.HasDescription ? request.Description : entity.Description;
        var startDate = request.HasStartDate ? request.StartDate : entity.StartDate;
        var endDate = request.HasEndDate ? request.EndDate : entity.EndDate;

        Class.ValidateMetadata(entity.Code, name, description, startDate, endDate);
        if (entity.Status == ClassStatus.Active && request.HasStartDate && startDate != entity.StartDate)
            throw new ClassRuleException("immutable_start_date", "Start date cannot change after activation.");
        if (entity.Status == ClassStatus.Active && (startDate is null || endDate is null))
            throw new ClassRuleException("required_dates", "Active classes require both dates.");

        entity.Name = name.Trim();
        entity.Description = description?.Trim();
        entity.StartDate = startDate;
        entity.EndDate = endDate;
        entity.Touch(adminId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Class {ClassId} updated by {AdminId}; version {Version}", id, adminId, entity.Version);
        return entity.Detail();
    }
}
using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.CreateClass;

public sealed class CreateClassHandler(
    IClassRepository db,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<CreateClassHandler> logger) : IRequestHandler<CreateClassCommand, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(CreateClassCommand request, CancellationToken ct)
    {
        var codeInput = request.Code;
        var name = request.Name;
        var description = request.Description;
        var startDate = request.StartDate;
        var endDate = request.EndDate;
        var adminId = request.AdminId;

        Class.ValidateMetadata(codeInput, name, description, startDate, endDate);
        var code = codeInput.Trim();
        if (await db.CodeExistsAsync(code, ct))
            throw new ClassRuleException("duplicate_code", "Class code already exists.", 409);
        var now = clock.GetUtcNow().UtcDateTime;
        var entity = new Class
        {
            Code = code,
            Name = name.Trim(),
            Description = description?.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = adminId,
            UpdatedBy = adminId
        };
        db.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Class {ClassId} created by {AdminId}", entity.Id, adminId);
        return entity.Detail();
    }
}

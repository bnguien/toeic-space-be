using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Modules;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Modules.Commands.CreateModule;

public sealed class CreateModuleHandler(ICourseRepository courses, ICourseModuleRepository modules,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<CreateModuleHandler> logger)
    : IRequestHandler<CreateModuleCommand, ModuleDto>
{
    public async Task<ModuleDto> Handle(CreateModuleCommand request, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var course = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        if (course.Status == CourseStatus.Archived) throw ContentRuleException.InvalidState();
        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await modules.ListAsync(course.Id, tracking: false, ct);
        var position = request.SortOrder ?? existing.Count + 1;
        if (position < 1 || position > existing.Count + 1)
            throw new ContentRuleException("invalid_sort_order", "Sort order must be within the current module range.");
        var module = new CourseModule { CourseId = course.Id, SortOrder = position, CreatedAt = now };
        module.SetMetadata(request.Title, request.Description, now);
        course.Touch(now);
        await unitOfWork.SaveChangesAsync(ct); // Claim course version before writing positions.
        if (position <= existing.Count)
        {
            var targets = existing.ToDictionary(x => x.Id,
                x => x.SortOrder >= position ? x.SortOrder + 1 : x.SortOrder);
            await ModuleOrdering.ReindexAsync(modules, unitOfWork, course.Id, targets, existing.Count + 1, now, ct);
        }
        modules.Add(module);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Created module {ModuleId} at position {SortOrder} in course {CourseId}", module.Id, position, course.Id);
        return module.ToDto();
    }
}

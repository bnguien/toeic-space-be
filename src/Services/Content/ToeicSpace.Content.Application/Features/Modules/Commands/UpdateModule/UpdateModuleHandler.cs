using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Modules;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Modules.Commands.UpdateModule;

public sealed class UpdateModuleHandler(ICourseRepository courses, ICourseModuleRepository modules,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<UpdateModuleHandler> logger)
    : IRequestHandler<UpdateModuleCommand, ModuleDto>
{
    public async Task<ModuleDto> Handle(UpdateModuleCommand request, CancellationToken ct)
    {
        var course = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        if (course.Status == CourseStatus.Archived) throw ContentRuleException.InvalidState();
        var module = await modules.GetAsync(course.Id, request.ModuleId, ct)
            ?? throw new ContentRuleException("module_not_found", "Module not found in this course.", 404);
        var now = clock.GetUtcNow().UtcDateTime;
        module.SetMetadata(request.Title, request.Description, now);
        course.Touch(now);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Updated module {ModuleId} in course {CourseId}", module.Id, course.Id);
        return module.ToDto();
    }
}

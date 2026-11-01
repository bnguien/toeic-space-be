using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Modules.Commands.DeleteModule;

public sealed class DeleteModuleHandler(ICourseRepository courses, ICourseModuleRepository modules,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<DeleteModuleHandler> logger)
    : IRequestHandler<DeleteModuleCommand>
{
    public async Task Handle(DeleteModuleCommand request, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var course = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        if (course.Status == CourseStatus.Archived) throw ContentRuleException.InvalidState();
        var module = await modules.GetAsync(course.Id, request.ModuleId, ct)
            ?? throw new ContentRuleException("module_not_found", "Module not found in this course.", 404);
        if (await modules.HasLessonsAsync(module.Id, ct))
            throw new ContentRuleException("module_in_use", "Module contains lessons and cannot be deleted.", 409);
        course.Touch(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        modules.Remove(module);
        await unitOfWork.SaveChangesAsync(ct);
        var remaining = await modules.ListAsync(course.Id, tracking: false, ct);
        var positions = remaining.Select((x, index) => (x.Id, Position: index + 1))
            .ToDictionary(x => x.Id, x => x.Position);
        await ModuleOrdering.ReindexAsync(modules, unitOfWork, course.Id,
            positions, remaining.Count + 1, clock.GetUtcNow().UtcDateTime, ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Deleted module {ModuleId} from course {CourseId}", module.Id, course.Id);
    }
}

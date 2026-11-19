using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Modules;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Modules.Commands.ReorderModules;

public sealed class ReorderModulesHandler(ICourseRepository courses, ICourseModuleRepository modules,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<ReorderModulesHandler> logger)
    : IRequestHandler<ReorderModulesCommand, IReadOnlyList<ModuleDto>>
{
    public async Task<IReadOnlyList<ModuleDto>> Handle(ReorderModulesCommand request, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var course = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        if (course.Status == CourseStatus.Archived) throw ContentRuleException.InvalidState();
        var current = await modules.ListAsync(course.Id, tracking: false, ct);
        if (request.ModuleIds is null || request.ModuleIds.Count != current.Count ||
            request.ModuleIds.Distinct().Count() != request.ModuleIds.Count ||
            !request.ModuleIds.ToHashSet().SetEquals(current.Select(x => x.Id)))
            throw new ContentRuleException("invalid_module_order", "Provide each module ID of this course exactly once.");
        course.Touch(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        var positions = request.ModuleIds.Select((id, index) => (id, Position: index + 1))
            .ToDictionary(x => x.id, x => x.Position);
        await ModuleOrdering.ReindexAsync(modules, unitOfWork, course.Id,
            positions, current.Count + 1, clock.GetUtcNow().UtcDateTime, ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Reordered {ModuleCount} modules in course {CourseId}", current.Count, course.Id);
        return (await modules.ListAsync(course.Id, tracking: false, ct)).Select(x => x.ToDto()).ToArray();
    }
}

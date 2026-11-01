using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.ArchiveCourse;

public sealed class ArchiveCourseHandler(ICourseRepository courses, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<ArchiveCourseHandler> logger) : IRequestHandler<ArchiveCourseCommand, CourseDto>
{
    public async Task<CourseDto> Handle(ArchiveCourseCommand request, CancellationToken ct)
    {
        var entity = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        entity.ChangeStatus(CourseStatus.Archived, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Archived course {CourseId}", entity.Id);
        return entity.ToDto();
    }
}

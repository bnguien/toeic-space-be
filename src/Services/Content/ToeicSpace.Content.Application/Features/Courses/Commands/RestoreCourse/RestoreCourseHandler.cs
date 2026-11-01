using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.RestoreCourse;

public sealed class RestoreCourseHandler(ICourseRepository courses, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<RestoreCourseHandler> logger) : IRequestHandler<RestoreCourseCommand, CourseDto>
{
    public async Task<CourseDto> Handle(RestoreCourseCommand request, CancellationToken ct)
    {
        var entity = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        entity.ChangeStatus(CourseStatus.Draft, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Restored course {CourseId} to Draft", entity.Id);
        return entity.ToDto();
    }
}

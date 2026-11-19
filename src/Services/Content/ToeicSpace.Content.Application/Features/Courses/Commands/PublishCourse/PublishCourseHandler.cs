using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.PublishCourse;

public sealed class PublishCourseHandler(ICourseRepository courses, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<PublishCourseHandler> logger) : IRequestHandler<PublishCourseCommand, CourseDto>
{
    public async Task<CourseDto> Handle(PublishCourseCommand request, CancellationToken ct)
    {
        var entity = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        entity.ChangeStatus(CourseStatus.Published, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Published course {CourseId}", entity.Id);
        return entity.ToDto();
    }
}

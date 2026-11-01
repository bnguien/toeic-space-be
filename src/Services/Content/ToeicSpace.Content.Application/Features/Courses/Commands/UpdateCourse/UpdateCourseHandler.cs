using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCourse;

public sealed class UpdateCourseHandler(ICourseRepository courses, IThumbnailUploadRepository uploads,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<UpdateCourseHandler> logger)
    : IRequestHandler<UpdateCourseCommand, CourseDto>
{
    public async Task<CourseDto> Handle(UpdateCourseCommand request, CancellationToken ct)
    {
        var entity = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        if (entity.Status == CourseStatus.Archived) throw ContentRuleException.InvalidState();
        if (request.Code is not null && request.Code.Trim() != entity.Code)
            throw new ContentRuleException("immutable_code", "Course code cannot be changed.");
        var slug = request.Slug?.Trim() ?? string.Empty;
        if (slug != entity.Slug && await courses.SlugExistsAsync(slug, entity.Id, ct))
            throw new ContentRuleException("duplicate_slug", "Course slug already exists.", 409);
        if (request.ThumbnailKey is not null && request.ThumbnailKey != entity.ThumbnailKey
            && !await uploads.IsConfirmedByAsync(request.ThumbnailKey, request.AdminId, ct))
            throw new ContentRuleException("unconfirmed_thumbnail", "Thumbnail must be confirmed by the current Admin before use.", 409);
        entity.Title = request.Title?.Trim() ?? string.Empty;
        entity.Slug = slug;
        entity.ShortDescription = request.ShortDescription?.Trim();
        entity.Description = request.Description?.Trim();
        entity.ThumbnailKey = request.ThumbnailKey;
        entity.ExamType = request.ExamType;
        entity.Level = request.Level;
        entity.TargetScore = request.TargetScore;
        entity.Skills = request.Skills ?? [];
        entity.Objectives = request.Objectives ?? [];
        entity.Prerequisites = request.Prerequisites ?? [];
        entity.Validate(publishing: entity.Status == CourseStatus.Published);
        entity.Touch(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Updated course {CourseId}", entity.Id);
        return entity.ToDto();
    }
}

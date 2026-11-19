using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.CreateCourse;

public sealed class CreateCourseHandler(ICourseRepository courses, IThumbnailUploadRepository uploads,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<CreateCourseHandler> logger)
    : IRequestHandler<CreateCourseCommand, CourseDto>
{
    public async Task<CourseDto> Handle(CreateCourseCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var entity = new Course
        {
            Code = request.Code?.Trim() ?? string.Empty, Title = request.Title?.Trim() ?? string.Empty,
            Slug = request.Slug?.Trim() ?? string.Empty, ShortDescription = request.ShortDescription?.Trim(),
            Description = request.Description?.Trim(), ThumbnailKey = request.ThumbnailKey,
            ExamType = request.ExamType, Level = request.Level, TargetScore = request.TargetScore,
            Skills = request.Skills ?? [], Objectives = request.Objectives ?? [], Prerequisites = request.Prerequisites ?? [],
            CreatedAt = now, UpdatedAt = now
        };
        entity.Validate();
        if (await courses.CodeExistsAsync(entity.Code, null, ct))
            throw new ContentRuleException("duplicate_code", "Course code already exists.", 409);
        if (await courses.SlugExistsAsync(entity.Slug, null, ct))
            throw new ContentRuleException("duplicate_slug", "Course slug already exists.", 409);
        if (entity.ThumbnailKey is not null && !await uploads.IsConfirmedByAsync(entity.ThumbnailKey, request.AdminId, ct))
            throw new ContentRuleException("unconfirmed_thumbnail", "Thumbnail must be confirmed by the current Admin before use.", 409);
        courses.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Created course {CourseId}", entity.Id);
        return entity.ToDto();
    }
}

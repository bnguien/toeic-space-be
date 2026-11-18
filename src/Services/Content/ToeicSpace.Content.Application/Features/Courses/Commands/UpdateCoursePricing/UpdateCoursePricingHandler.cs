using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCoursePricing;

public sealed class UpdateCoursePricingHandler(ICourseRepository courses, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<UpdateCoursePricingCommand, CourseDto>
{
    public async Task<CourseDto> Handle(UpdateCoursePricingCommand request, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var course = await courses.GetLockedAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        if (request.Capacity is { } capacity && capacity < await courses.OccupiedSlotsAsync(course.Id, ct))
            throw new ContentRuleException("capacity_below_occupied", "Capacity cannot be below enrollments and reserved slots.", 409);
        course.Price = request.Price;
        course.Capacity = request.Capacity;
        course.Validate(publishing: course.Status == Domain.Enums.CourseStatus.Published);
        course.Touch(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return course.ToDto();
    }
}

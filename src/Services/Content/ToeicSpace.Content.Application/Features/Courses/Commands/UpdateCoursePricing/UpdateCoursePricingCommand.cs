using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCoursePricing;

public sealed record UpdateCoursePricingCommand(Guid CourseId, decimal Price, int? Capacity) : IRequest<CourseDto>;

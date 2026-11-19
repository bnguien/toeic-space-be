using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.PublishCourse;

public sealed record PublishCourseCommand(Guid CourseId) : IRequest<CourseDto>;

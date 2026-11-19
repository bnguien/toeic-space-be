using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.RestoreCourse;

public sealed record RestoreCourseCommand(Guid CourseId) : IRequest<CourseDto>;

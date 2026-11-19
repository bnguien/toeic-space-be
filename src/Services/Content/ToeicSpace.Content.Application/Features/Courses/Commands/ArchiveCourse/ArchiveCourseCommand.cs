using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.ArchiveCourse;

public sealed record ArchiveCourseCommand(Guid CourseId) : IRequest<CourseDto>;

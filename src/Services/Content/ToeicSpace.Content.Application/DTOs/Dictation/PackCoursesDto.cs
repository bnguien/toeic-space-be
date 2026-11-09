namespace ToeicSpace.Content.Application.DTOs.Dictation;

public sealed record PackCourseDto(int Id, string Code, string Title, int Order, int Level, int ItemCount,
    int PassedCount);

public sealed record PackCoursesDto(string Pack, IReadOnlyList<PackCourseDto> Courses);

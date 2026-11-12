using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Mappings;

public static class CourseMappingExtensions
{
    public static CourseDto ToDto(this Course x) => new(x.Id, x.Code, x.Title, x.Slug, x.ShortDescription,
        x.Description, x.ThumbnailKey, x.ExamType, x.Level, x.TargetScore,
        x.Skills.ToArray(), x.Objectives.ToArray(), x.Prerequisites.ToArray(), x.Status,
        x.CreatedAt, x.UpdatedAt, x.PublishedAt, Price: x.Price, Capacity: x.Capacity);
}

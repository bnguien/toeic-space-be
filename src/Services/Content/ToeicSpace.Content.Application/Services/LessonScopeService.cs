using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Services;

public sealed class LessonScopeService(ICourseRepository courses, ICourseModuleRepository modules, ILessonRepository lessons)
{
    public async Task<(Course Course, CourseModule Module)> GetModuleAsync(Guid courseId, Guid moduleId,
        bool writable, CancellationToken ct)
    {
        var course = await courses.GetAsync(courseId, ct) ?? throw ContentRuleException.NotFound();
        var module = await modules.GetAsync(courseId, moduleId, ct)
            ?? throw new ContentRuleException("module_not_found", "Module not found in this course.", 404);
        if (writable && course.Status == CourseStatus.Archived) throw ContentRuleException.InvalidState();
        return (course, module);
    }

    public async Task<(Course Course, CourseModule Module, Lesson Lesson)> GetLessonAsync(
        Guid courseId, Guid moduleId, Guid lessonId, bool writable, CancellationToken ct)
    {
        var (course, module) = await GetModuleAsync(courseId, moduleId, writable, ct);
        var lesson = await lessons.GetAsync(module.Id, lessonId, ct)
            ?? throw new ContentRuleException("lesson_not_found", "Lesson not found in this module.", 404);
        return (course, module, lesson);
    }
}

using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClassById;

public sealed class GetMyEnrolledClassByIdHandler(IClassRepository classes)
    : IRequestHandler<GetMyEnrolledClassByIdQuery, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(GetMyEnrolledClassByIdQuery request, CancellationToken ct)
    {
        var entity = await classes.GetStudentDetailAsync(request.StudentId, request.ClassId, ct)
            ?? throw ClassRuleException.NotFound();
        return new ClassDetailDto(entity.Class.Summary() with { TeacherName = entity.TeacherName }, []);
    }
}

using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClassById;

public sealed class GetClassByIdHandler(IClassRepository db) : IRequestHandler<GetClassByIdQuery, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(GetClassByIdQuery request, CancellationToken ct)
    {
        var id = request.ClassId;
        var teacherId = request.TeacherId;

        var entity = await db.GetDetailAsync(id, ct) ?? throw ClassRuleException.NotFound();
        if (teacherId is { } tid && !entity.Instructors.Any(x => x.TeacherId == tid && x.UnassignedAt == null))
            throw new ClassRuleException("forbidden_class", "You are not assigned to this class.", 403);
        var detail = entity.Detail();
        return teacherId is null ? detail : detail with { InstructorHistory = [] };
    }
}

using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Mappings;

public static class ClassMappingExtensions
{
    public static ClassDto Summary(this Class x) => new(x.Id, x.Code, x.Name, x.Description, x.Status, x.StartDate,
        x.EndDate, x.CreatedAt, x.CreatedBy, x.UpdatedAt, x.UpdatedBy,
        x.Instructors.SingleOrDefault(i => i.UnassignedAt == null)?.TeacherId, x.ArchivedFromStatus);

    public static ClassDetailDto Detail(this Class x) => new(x.Summary(),
        x.Instructors.OrderBy(i => i.AssignedAt).Select(i =>
                new InstructorDto(i.Id, i.TeacherId, i.AssignedAt, i.AssignedBy, i.UnassignedAt, i.UnassignedBy))
            .ToArray());
}

using ToeicSpace.Content.Application.DTOs.Modules;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Mappings;

public static class ModuleMappingExtensions
{
    public static ModuleDto ToDto(this CourseModule x) =>
        new(x.Id, x.CourseId, x.Title, x.Description, x.SortOrder, x.CreatedAt, x.UpdatedAt);

    public static ModuleSummaryDto ToSummary(this CourseModule x) => new(x.Id, x.Title, x.SortOrder);
}

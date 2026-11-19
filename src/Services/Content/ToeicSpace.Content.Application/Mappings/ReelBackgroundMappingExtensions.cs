using ToeicSpace.Content.Application.DTOs.ReelBackgrounds;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Mappings;

public static class ReelBackgroundMappingExtensions
{
    public static ReelBackgroundDto ToDto(this ReelBackground x) =>
        new(x.Id, x.VideoUrl, x.PosterUrl, x.PageUrl, x.Author);
}

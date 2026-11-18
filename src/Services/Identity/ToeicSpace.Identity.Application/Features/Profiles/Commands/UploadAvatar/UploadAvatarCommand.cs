using System.Text.Json.Serialization;

namespace ToeicSpace.Identity.Application.Features.Profiles.Commands.UploadAvatar;

public sealed record UploadAvatarCommand(
    string ContentType
) : IRequest<string>
{
    [JsonIgnore]
    public Guid UserId { get; set; }
}

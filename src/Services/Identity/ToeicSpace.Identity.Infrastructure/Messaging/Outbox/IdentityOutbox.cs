using System.Text.Json;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Identity.Infrastructure.Messaging.Outbox;

internal static class IdentityOutbox
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static OutboxMessage Create(IIntegrationEvent message)
    {
        var type = message.GetType();
        return new OutboxMessage
        {
            Id = message.Id,
            Type = type.AssemblyQualifiedName ?? type.FullName!,
            Content = JsonSerializer.Serialize(message, type, SerializerOptions),
            OccurredOn = message.OccurredOn
        };
    }
}

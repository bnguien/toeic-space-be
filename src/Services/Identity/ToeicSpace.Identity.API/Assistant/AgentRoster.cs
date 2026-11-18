using System.Text.Json;

namespace ToeicSpace.Identity.API.Assistant;

/// <summary>
/// One SeasonedAgents agent as the admin panel may see it. Internal IDs (<c>id</c>, <c>orgId</c>), the
/// <c>instructions</c> prompt and the <c>score</c> are never copied.
/// </summary>
/// <param name="Persona">The upstream persona: usually a string, otherwise its JSON value as is.</param>
public sealed record AssistantAgent(
    string Code,
    string Name,
    string? Kind,
    string? Role,
    IReadOnlyList<string> Capabilities,
    string? Status,
    object? Persona,
    long Uses,
    long Wins);

/// <summary>Turns the SeasonedAgents roster (<c>GET agents</c>) into the list the admin panel shows.</summary>
public static class AgentRoster
{
    private const string CoreRole = "core";
    private const string ActiveStatus = "active";

    /// <summary>
    /// Keeps active, non-core agents. Accepts a JSON array or an object wrapping it in <c>agents</c>,
    /// <c>items</c> or <c>data</c>. Returns null when the body is not such a roster.
    /// </summary>
    public static IReadOnlyList<AssistantAgent>? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var list = FindList(document.RootElement);

            return list is { } agents
                ? agents.EnumerateArray().Select(ToAgent).OfType<AssistantAgent>().Where(IsVisible).ToList()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsVisible(AssistantAgent agent)
        => !string.Equals(agent.Role, CoreRole, StringComparison.OrdinalIgnoreCase)
            && string.Equals(agent.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase);

    private static JsonElement? FindList(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "agents", "items", "data" })
            {
                if (root.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    return list;
                }
            }
        }

        return null;
    }

    private static AssistantAgent? ToAgent(JsonElement agent)
    {
        if (agent.ValueKind != JsonValueKind.Object || Text(agent, "code") is not { Length: > 0 } code)
        {
            return null;
        }

        var capabilities = agent.TryGetProperty("capabilities", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToList()
            : [];

        object? persona = agent.TryGetProperty("persona", out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => value.Clone()
            }
            : null;

        return new AssistantAgent(
            code,
            Text(agent, "name") ?? code,
            Text(agent, "kind"),
            Text(agent, "role"),
            capabilities,
            Text(agent, "status"),
            persona,
            Number(agent, "uses"),
            Number(agent, "wins"));
    }

    private static string? Text(JsonElement agent, string name)
        => agent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long Number(JsonElement agent, string name)
        => agent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number)
                ? number
                : 0;
}

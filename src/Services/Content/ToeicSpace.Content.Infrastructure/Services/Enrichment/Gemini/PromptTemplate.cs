using System.Reflection;

namespace ToeicSpace.Content.Infrastructure.Services.Enrichment.Gemini;

/// <summary>
/// Prompt files embedded from <c>Services/Enrichment/Prompts/*.md</c>: system text, a line <c>===USER===</c>, then the
/// user template with <c>{{words}}</c>, <c>{{text}}</c> and <c>{{context}}</c> placeholders.
/// </summary>
public sealed record PromptTemplate(string System, string User)
{
    private const string Separator = "===USER===";

    public static PromptTemplate Load(string name)
    {
        var resource = $"ToeicSpace.Content.Infrastructure.Prompts.{name}.md";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Prompt resource {resource} is missing.");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd().Replace("\r\n", "\n");
        var at = content.IndexOf(Separator, StringComparison.Ordinal);
        if (at < 0) throw new InvalidOperationException($"Prompt {name} has no {Separator} line.");
        return new PromptTemplate(content[..at].Trim(), content[(at + Separator.Length)..].Trim());
    }

    public string Render(IReadOnlyDictionary<string, string> values) =>
        values.Aggregate(User, (text, value) => text.Replace("{{" + value.Key + "}}", value.Value));
}

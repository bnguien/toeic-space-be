namespace ToeicSpace.Content.Application.Models.News;

/// <summary>Configuration section <c>News</c>.</summary>
public sealed class NewsSettings
{
    public const string SectionName = "News";

    /// <summary>Folder of the cover store; relative paths are resolved against the API content root.</summary>
    public string CoverDirectory { get; set; } = "App_Data/news/covers";

    /// <summary>Public URL prefix of the covers as seen by the browser (through the gateway).</summary>
    public string CoverBaseUrl { get; set; } = "/content/api/v1/news/covers";
}

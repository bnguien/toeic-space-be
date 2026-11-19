using ToeicSpace.Content.Application.Models.Videos;

namespace ToeicSpace.Content.Application.Interfaces.Services.Video;

/// <summary>English dictionary details for the word card (synonyms, antonyms, senses, US IPA).</summary>
public interface IWordDetailDictionary
{
    /// <summary>Null when the source failed (cache briefly); <see cref="WordDetailResult.NotFound"/> for unknown words.</summary>
    Task<WordDetailResult?> LookupAsync(string word, CancellationToken ct);
}

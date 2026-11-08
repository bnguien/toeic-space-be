using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.API.Middlewares;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.Features.Speech.Queries.GetSpeechAudio;
using ToeicSpace.Content.Infrastructure.Services.Speech;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>
/// Shared neural speech for word and sentence audio across features: <c>GET api/v1/speech?text=&amp;voice=</c> →
/// <c>audio/mpeg</c> (cached on disk, 7-day Cache-Control). 503 <c>SPEECH_UNAVAILABLE</c> when the upstream fails
/// so clients fall back to the browser's speechSynthesis.
/// </summary>
[ApiController]
[Route("api/v1/speech")]
[AllowAnonymous]
public sealed class SpeechController(ISender sender, IOptions<SpeechOptions> speech) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting(DictationRateLimits.Speech)]
    public async Task<IActionResult> Get([FromQuery] string? text, [FromQuery] string? voice, [FromQuery] double? rate,
        CancellationToken ct)
    {
        var clip = await sender.Send(new GetSpeechAudioQuery(text, voice, rate), ct);
        PublicCacheControl.Set(HttpContext, speech.Value.ClientCacheSeconds);
        Response.Headers.ContentDisposition = $"inline; filename=\"{clip.FileName}\"";
        return PhysicalFile(clip.FilePath, "audio/mpeg", enableRangeProcessing: true);
    }
}

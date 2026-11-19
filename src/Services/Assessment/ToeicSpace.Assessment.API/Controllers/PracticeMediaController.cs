using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.Application.Interfaces.Storage;

namespace ToeicSpace.Assessment.API.Controllers;

[ApiController]
[Route("api/v1/practice-media")]
public sealed class PracticeMediaController(IAttemptMediaStore store) : ControllerBase
{
    [HttpGet("{hash}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string hash, CancellationToken ct)
    {
        var (content, type) = await store.OpenAsync(hash, ct);
        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return File(content, type, enableRangeProcessing: true);
    }
}

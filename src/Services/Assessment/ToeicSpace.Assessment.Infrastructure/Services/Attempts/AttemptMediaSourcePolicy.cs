using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using ToeicSpace.Assessment.Application.Common.Exceptions;

namespace ToeicSpace.Assessment.Infrastructure.Services.Attempts;

// HttpClient's automatic redirects would bypass the origin check. Validate every hop instead.
internal sealed class AttemptMediaSourcePolicy
{
    private readonly HashSet<string> origins = new(StringComparer.OrdinalIgnoreCase);

    public AttemptMediaSourcePolicy(IEnumerable<string> allowedOrigins)
    {
        foreach (var value in allowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0
                || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new PracticeMediaException("source_configuration");
            origins.Add(uri.GetLeftPart(UriPartial.Authority));
        }
        if (origins.Count == 0) throw new PracticeMediaException("source_configuration");
    }

    public Uri Validate(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0
            || !origins.Contains(uri.GetLeftPart(UriPartial.Authority)))
            throw new PracticeMediaException("untrusted_or_invalid_source");
        return uri;
    }

    public async Task<HttpResponseMessage> GetAsync(HttpClient http, Uri source,
        RetainedMediaSource? retained, CancellationToken ct)
    {
        var current = source;
        for (var hop = 0; hop <= 5; hop++)
        {
            Validate(current.AbsoluteUri);
            var response = await SendAsync(http, current, retained, ct);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Found
                or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
                return response;

            var location = response.Headers.Location;
            response.Dispose();
            if (hop == 5 || location == null || !Uri.TryCreate(current, location, out var next))
                throw new PracticeMediaException("invalid_source_redirect");
            if (current.Scheme == Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttps)
                throw new PracticeMediaException("untrusted_or_invalid_source");
            current = next;
        }
        throw new PracticeMediaException("invalid_source_redirect");
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient http, Uri uri,
        RetainedMediaSource? retained, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            // Validators belong to the final resource, never an unrelated redirect target.
            if (retained?.FinalSourceHash == Reference(uri.AbsoluteUri))
            {
                if (EntityTagHeaderValue.TryParse(retained.ETag, out var tag) && !tag.IsWeak)
                    request.Headers.IfNoneMatch.Add(tag);
                else if (retained.LastModified.HasValue)
                    request.Headers.IfModifiedSince = retained.LastModified;
            }
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException) when (attempt == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
                continue;
            }
            if (response.StatusCode == HttpStatusCode.NotModified
                && request.Headers.IfNoneMatch.Count == 0 && !request.Headers.IfModifiedSince.HasValue)
            {
                response.Dispose();
                throw new PracticeMediaException("invalid_source_response");
            }
            if (attempt != 0 || response.StatusCode is not (HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
                return response;

            var delay = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                ?? TimeSpan.FromMilliseconds(250);
            // Do not retry earlier than the server requested or outlive the transfer budget.
            if (delay > TimeSpan.FromSeconds(2)) return response;
            response.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(delay.TotalSeconds, 0.25)), ct);
        }
    }

    public static string Reference(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

// Stored only as an optimization in the existing content cache. The bytes live in immutable R2 objects.
internal sealed record RetainedMediaSource(string Hash, string FinalSourceHash, string? ETag,
    DateTimeOffset? LastModified);

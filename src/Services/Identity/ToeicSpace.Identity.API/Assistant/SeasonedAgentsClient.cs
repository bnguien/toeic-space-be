using System.Net.Http.Json;

namespace ToeicSpace.Identity.API.Assistant;

public sealed record ActingAdmin(string Id, string Email);

public sealed record AssistantReply(int Status, string Body);

/// <summary>
/// Calls SeasonedAgents for the signed-in administrator: the integration key decides the organisation, the acting
/// headers say which admin is asking. Answers are capped at 1 MB.
/// </summary>
public sealed class SeasonedAgentsClient(HttpClient http, SeasonedAgentsOptions options)
{
    private const int MaxAnswerBytes = 1_048_576;

    public bool Enabled => !string.IsNullOrWhiteSpace(options.ClientKey) && http.BaseAddress is not null;

    public async Task<AssistantReply> SendAsync(
        HttpMethod method, string path, object? body, ActingAdmin admin, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        request.Headers.TryAddWithoutValidation("X-Hv-Client-Key", options.ClientKey);
        request.Headers.TryAddWithoutValidation("X-Hv-Acting-User", admin.Id);
        request.Headers.TryAddWithoutValidation("X-Hv-Acting-Email", admin.Email);
        request.Headers.TryAddWithoutValidation("X-Hv-Acting-Role", "admin");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16_384];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxAnswerBytes)
            {
                throw new HttpRequestException("The assistant answer is too large.");
            }

            buffer.Write(chunk, 0, read);
        }

        return new AssistantReply((int)response.StatusCode, System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}

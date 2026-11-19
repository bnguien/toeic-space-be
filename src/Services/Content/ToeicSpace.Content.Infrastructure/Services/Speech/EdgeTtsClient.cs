using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ToeicSpace.Content.Infrastructure.Services.Speech;

/// <summary>
/// Client of Microsoft Edge's online read-aloud service (port of edge-tts 7.3.1 <c>Communicate</c>): one websocket
/// per text, a <c>speech.config</c> message (mp3 24 kHz 48 kbit/s mono, sentence boundaries), an SSML message with
/// voice and rate, then binary <c>Path:audio</c> frames until <c>turn.end</c>. A 403 on the handshake is usually a
/// wrong local clock: the server <c>Date</c> header corrects the skew and the request is retried once.
/// </summary>
public sealed partial class EdgeTtsClient(IOptions<SpeechOptions> options, TimeProvider clock, ILogger<EdgeTtsClient> logger)
{
    private const string BaseUrl = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";
    private const string ChromiumFullVersion = "143.0.3650.75";
    private const string ChromiumMajorVersion = "143";
    private const string SecMsGecVersion = "2-" + ChromiumFullVersion;
    private const string OutputFormat = "audio-24khz-48kbitrate-mono-mp3";
    private const int MaxTextBytes = 4096;

    private static readonly string UserAgent =
        $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{ChromiumMajorVersion}.0.0.0 Safari/537.36 Edg/{ChromiumMajorVersion}.0.0.0";

    /// <summary>Seconds added to the local clock (shared by every request, like edge-tts' <c>DRM.clock_skew_seconds</c>).</summary>
    private static double _clockSkewSeconds;

    [GeneratedRegex(@"^([a-z]{2,})-([A-Z]{2,})-(.+Neural)$")]
    private static partial Regex ShortVoiceName();

    /// <summary>Synthesizes <paramref name="text"/> and returns the mp3 bytes.</summary>
    public async Task<byte[]> SynthesizeAsync(string text, string voiceName, string rate, CancellationToken ct)
    {
        var escaped = EscapeXml(RemoveIncompatibleCharacters(text));
        if (Encoding.UTF8.GetByteCount(escaped) > MaxTextBytes)
            throw new ArgumentException("Text is too long for one speech request.", nameof(text));
        try
        {
            return await StreamAsync(escaped, voiceName, rate, ct);
        }
        catch (SpeechHandshakeException ex) when (ex.Status == HttpStatusCode.Forbidden && AdjustClockSkew(ex.ServerDate))
        {
            return await StreamAsync(escaped, voiceName, rate, ct);
        }
        catch (Exception ex) when (ex is WebSocketException or IOException && !ct.IsCancellationRequested)
        {
            // The service sometimes drops a connection mid-stream (several texts at once): one quick retry.
            logger.LogInformation("Speech stream dropped ({Reason}); retrying once", ex.Message);
            await Task.Delay(300, ct);
            return await StreamAsync(escaped, voiceName, rate, ct);
        }
    }

    /// <summary>Like Python's <c>xml.sax.saxutils.escape</c>: only &amp;, &lt; and &gt;.</summary>
    public static string EscapeXml(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private bool AdjustClockSkew(string? serverDate)
    {
        if (serverDate is null
            || !DateTimeOffset.TryParse(serverDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var server))
            return false;
        var local = clock.GetUtcNow().ToUnixTimeMilliseconds() / 1000.0 + _clockSkewSeconds;
        _clockSkewSeconds += server.ToUnixTimeMilliseconds() / 1000.0 - local;
        logger.LogWarning("Speech service rejected the token; clock skew adjusted to {Skew:F0}s", _clockSkewSeconds);
        return true;
    }

    private async Task<byte[]> StreamAsync(string escapedText, string voiceName, string rate, CancellationToken ct)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.SynthesisTimeoutSeconds));
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        socket.Options.SetRequestHeader("Pragma", "no-cache");
        socket.Options.SetRequestHeader("Cache-Control", "no-cache");
        socket.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        socket.Options.SetRequestHeader("User-Agent", UserAgent);
        socket.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
        socket.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        socket.Options.SetRequestHeader("Cookie", $"muid={Convert.ToHexString(RandomNumberGenerator.GetBytes(16))};");

        var unixSeconds = clock.GetUtcNow().ToUnixTimeMilliseconds() / 1000.0 + _clockSkewSeconds;
        var url = new Uri($"wss://{BaseUrl}/edge/v1?TrustedClientToken={SecMsGecToken.TrustedClientToken}"
            + $"&ConnectionId={ConnectId()}&Sec-MS-GEC={SecMsGecToken.Generate(unixSeconds)}&Sec-MS-GEC-Version={SecMsGecVersion}");

        using (var connect = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token))
        {
            connect.CancelAfter(TimeSpan.FromSeconds(settings.ConnectTimeoutSeconds));
            try
            {
                await socket.ConnectAsync(url, connect.Token);
            }
            catch (WebSocketException ex)
            {
                string? date = null;
                if (socket.HttpResponseHeaders is { } headers && headers.TryGetValue("Date", out var values))
                    date = values.FirstOrDefault();
                throw new SpeechHandshakeException(socket.HttpStatusCode, date, ex);
            }
        }

        var timestamp = DateString();
        await SendTextAsync(socket,
            $"X-Timestamp:{timestamp}\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n"
            + "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{"
            + "\"sentenceBoundaryEnabled\":\"true\",\"wordBoundaryEnabled\":\"false\""
            + "},\"outputFormat\":\"" + OutputFormat + "\"}}}}\r\n", timeout.Token);
        var ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>"
            + $"<voice name='{FullVoiceName(voiceName)}'><prosody pitch='+0Hz' rate='{rate}' volume='+0%'>"
            + escapedText + "</prosody></voice></speak>";
        // "Z" after the JavaScript date string is not a mistake: Microsoft Edge sends it.
        await SendTextAsync(socket,
            $"X-RequestId:{ConnectId()}\r\nContent-Type:application/ssml+xml\r\nX-Timestamp:{timestamp}Z\r\nPath:ssml\r\n\r\n{ssml}",
            timeout.Token);

        using var audio = new MemoryStream();
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            message.SetLength(0);
            WebSocketReceiveResult result;
            using (var receive = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token))
            {
                receive.CancelAfter(TimeSpan.FromSeconds(settings.ReceiveTimeoutSeconds));
                do
                {
                    result = await socket.ReceiveAsync(buffer, receive.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        throw new InvalidOperationException("Speech service closed the connection before the end of the turn.");
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
            }

            var data = message.ToArray();
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var textMessage = Encoding.UTF8.GetString(data);
                var headerEnd = textMessage.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                var path = HeaderValue(headerEnd < 0 ? textMessage : textMessage[..headerEnd], "Path");
                if (path == "turn.end") break;
                if (path is not ("audio.metadata" or "response" or "turn.start"))
                    throw new InvalidOperationException($"Unknown path received: {path}");
                continue;
            }

            if (data.Length < 2) throw new InvalidOperationException("Binary message without header length.");
            var headerLength = (data[0] << 8) | data[1];
            if (headerLength > data.Length) throw new InvalidOperationException("Header length exceeds the message.");
            var headers = Encoding.UTF8.GetString(data, 0, headerLength);
            if (HeaderValue(headers, "Path") != "audio")
                throw new InvalidOperationException("Received binary message, but the path is not audio.");
            var contentType = HeaderValue(headers, "Content-Type");
            var payloadStart = Math.Min(data.Length, headerLength + 2);
            var payloadLength = data.Length - payloadStart;
            if (contentType is null)
            {
                if (payloadLength == 0) continue;
                throw new InvalidOperationException("Binary message with no Content-Type, but with data.");
            }
            if (contentType != "audio/mpeg") throw new InvalidOperationException("Unexpected audio Content-Type.");
            if (payloadLength == 0) throw new InvalidOperationException("Audio message without data.");
            audio.Write(data, payloadStart, payloadLength);
        }

        try
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // The server may already have dropped the connection after turn.end.
        }
        if (audio.Length == 0) throw new InvalidOperationException("No audio was received.");
        return audio.ToArray();
    }

    private static Task SendTextAsync(ClientWebSocket socket, string message, CancellationToken ct) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, ct);

    private static string? HeaderValue(string headers, string name)
    {
        foreach (var line in headers.Split("\r\n"))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            // Binary headers start with the 2-byte length: compare the end of the key only.
            if (line[..colon].EndsWith(name, StringComparison.Ordinal)) return line[(colon + 1)..];
        }
        return null;
    }

    private static string ConnectId() => Guid.NewGuid().ToString("N");

    /// <summary>JavaScript-style UTC date string, e.g. <c>Sat Nov 07 2026 10:00:00 GMT+0000 (Coordinated Universal Time)</c>.</summary>
    private string DateString() =>
        clock.GetUtcNow().UtcDateTime.ToString("ddd MMM dd yyyy HH:mm:ss", CultureInfo.InvariantCulture)
        + " GMT+0000 (Coordinated Universal Time)";

    /// <summary>"en-US-AndrewNeural" → "Microsoft Server Speech Text to Speech Voice (en-US, AndrewNeural)".</summary>
    public static string FullVoiceName(string voice)
    {
        var match = ShortVoiceName().Match(voice);
        if (!match.Success) return voice;
        var lang = match.Groups[1].Value;
        var region = match.Groups[2].Value;
        var name = match.Groups[3].Value;
        var dash = name.IndexOf('-');
        if (dash >= 0)
        {
            region = $"{region}-{name[..dash]}";
            name = name[(dash + 1)..];
        }
        return $"Microsoft Server Speech Text to Speech Voice ({lang}-{region}, {name})";
    }

    /// <summary>Control characters the service rejects (vertical tab…) become spaces.</summary>
    public static string RemoveIncompatibleCharacters(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            int code = chars[i];
            if (code is >= 0 and <= 8 or >= 11 and <= 12 or >= 14 and <= 31) chars[i] = ' ';
        }
        return new string(chars);
    }
}

/// <summary>The websocket handshake failed; carries the HTTP status and the server's Date header.</summary>
public sealed class SpeechHandshakeException(HttpStatusCode status, string? serverDate, Exception inner)
    : Exception($"Speech service handshake failed ({(int)status}).", inner)
{
    public HttpStatusCode Status { get; } = status;
    public string? ServerDate { get; } = serverDate;
}

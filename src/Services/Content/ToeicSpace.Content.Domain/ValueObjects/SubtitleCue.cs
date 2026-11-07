using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToeicSpace.Content.Domain.ValueObjects;

/// <summary>
/// One subtitle line, serialized as Studychill's compact array <c>[start, duration, text, offsets?]</c>: seconds,
/// and optional per-word offsets (seconds from the cue start) aligned with <c>text.Split(' ')</c> for karaoke.
/// </summary>
[JsonConverter(typeof(SubtitleCueJsonConverter))]
public sealed record SubtitleCue(double Start, double Duration, string Text, IReadOnlyList<double>? Offsets = null)
{
    public static double Round(double seconds) => Math.Round(seconds, 2, MidpointRounding.ToEven);
}

/// <summary>Reads/writes <see cref="SubtitleCue"/> as a JSON array.</summary>
public sealed class SubtitleCueJsonConverter : JsonConverter<SubtitleCue>
{
    public override SubtitleCue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("A cue must be an array.");
        double start = 0, duration = 0;
        string? text = null;
        List<double>? offsets = null;
        var index = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            switch (index)
            {
                case 0: start = ReadNumber(ref reader); break;
                case 1: duration = ReadNumber(ref reader); break;
                case 2: text = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException("Cue text must be a string."); break;
                case 3:
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        offsets = [];
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            offsets.Add(ReadNumber(ref reader));
                    }
                    else reader.Skip();
                    break;
                default: reader.Skip(); break;
            }
            index++;
        }
        if (index < 3 || text is null) throw new JsonException("A cue needs [start, duration, text].");
        return new SubtitleCue(start, duration, text, offsets);
    }

    public override void Write(Utf8JsonWriter writer, SubtitleCue value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Start);
        writer.WriteNumberValue(value.Duration);
        writer.WriteStringValue(value.Text);
        if (value.Offsets is { Count: > 0 } offsets)
        {
            writer.WriteStartArray();
            foreach (var offset in offsets) writer.WriteNumberValue(offset);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }

    private static double ReadNumber(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Number ? reader.GetDouble()
        : reader.TokenType == JsonTokenType.Null ? 0 : throw new JsonException("Expected a number in a cue.");
}

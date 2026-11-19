using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToeicSpace.Content.Application.Common.Json;

/// <summary>
/// A JSON property that may be absent (<see cref="IsSet"/> false = leave unchanged) or present with a value,
/// including <c>null</c>. Used by PATCH-like updates such as <c>{ "folderId": null }</c> (move to unfiled).
/// </summary>
[JsonConverter(typeof(OptionalJsonConverterFactory))]
public readonly struct Optional<T>
{
    public Optional(T? value)
    {
        IsSet = true;
        Value = value;
    }

    public bool IsSet { get; }
    public T? Value { get; }

    public static Optional<T> Unset => default;
    public static Optional<T> Of(T? value) => new(value);
}

public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(OptionalJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    {
        public override bool HandleNull => true;

        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null
                ? new Optional<T>(default)
                : new Optional<T>(JsonSerializer.Deserialize<T>(ref reader, options));

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
        {
            if (value.Value is null) writer.WriteNullValue();
            else JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}

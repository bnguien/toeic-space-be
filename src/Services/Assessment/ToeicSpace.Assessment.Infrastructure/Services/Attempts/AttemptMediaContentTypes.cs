using System.Buffers.Binary;

namespace ToeicSpace.Assessment.Infrastructure.Services.Attempts;

// Imported listening files do not all use the same HTTP MIME spelling. Keep the
// retained object's type canonical and require a known signature regardless of the HTTP label.
internal static class AttemptMediaContentTypes
{
    public static string? Normalize(string? contentType) => contentType?.Trim().ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" or "image/pjpeg" => "image/jpeg",
        "image/png" or "image/x-png" => "image/png",
        "image/gif" => "image/gif",
        "image/webp" => "image/webp",
        "image/avif" => "image/avif",
        "audio/mpeg" or "audio/mp3" or "audio/x-mp3" or "audio/mpeg3" or "audio/x-mpeg-3"
            or "audio/mpg" or "audio/x-mpeg" => "audio/mpeg",
        "audio/wav" or "audio/wave" or "audio/x-wav" or "audio/vnd.wave" => "audio/wav",
        "audio/ogg" => "audio/ogg",
        "audio/mp4" or "audio/m4a" or "audio/x-m4a" => "audio/mp4",
        "audio/aac" or "audio/aacp" or "audio/x-aac" => "audio/aac",
        "audio/webm" => "audio/webm",
        "audio/flac" or "audio/x-flac" => "audio/flac",
        _ => null
    };

    // A file name or a generic Content-Type is insufficient evidence of audio/image
    // content. In particular, HTML error pages must never become retained MP3s.
    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return "image/jpeg";
        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
            return "image/gif";
        if (bytes.Length >= 12 && (bytes[..4].SequenceEqual("RIFF"u8) || bytes[..4].SequenceEqual("RF64"u8)))
        {
            if (bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
            if (bytes.Slice(8, 4).SequenceEqual("WAVE"u8)) return "audio/wav";
        }
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual("fLaC"u8)) return "audio/flac";
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual("ADIF"u8)) return "audio/aac";
        if (bytes.Length >= 27 && bytes[..4].SequenceEqual("OggS"u8) && bytes[4] == 0)
        {
            var packet = 27 + bytes[26];
            if (bytes[26] > 0 && bytes.Length >= packet + 8
                && (bytes.Slice(packet, 8).SequenceEqual("OpusHead"u8)
                    || bytes.Slice(packet, 7).SequenceEqual("\u0001vorbis"u8)
                    || bytes.Slice(packet, 8).SequenceEqual("Speex   "u8)
                    || bytes.Slice(packet, 5).SequenceEqual("\u007fFLAC"u8)))
                return "audio/ogg";
        }
        if (bytes.Length >= 8 && bytes[..4].SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 })
            && bytes.IndexOf("webm"u8) >= 4)
            return "audio/webm";
        if (bytes.Length >= 16 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var boxSize = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            if (boxSize < 16) return null;
            // AVIF may advertise its type as a compatible brand rather than the major brand.
            var end = (int)Math.Min(boxSize, (uint)bytes.Length);
            for (var offset = 8; offset + 4 <= end; offset += 4)
            {
                if (offset == 12) continue; // Minor version is not a brand.
                var compatible = bytes.Slice(offset, 4);
                if (compatible.SequenceEqual("avif"u8) || compatible.SequenceEqual("avis"u8))
                    return "image/avif";
            }
            var brand = bytes.Slice(8, 4);
            if (brand.SequenceEqual("M4A "u8) || brand.SequenceEqual("M4B "u8)
                || brand.SequenceEqual("isom"u8) || brand.SequenceEqual("iso2"u8)
                || brand.SequenceEqual("iso3"u8) || brand.SequenceEqual("iso4"u8)
                || brand.SequenceEqual("iso5"u8) || brand.SequenceEqual("iso6"u8)
                || brand.SequenceEqual("mp41"u8) || brand.SequenceEqual("mp42"u8)
                || brand.SequenceEqual("dash"u8)) return "audio/mp4";
        }
        // ID3v2 header: a supported version and a sync-safe tag size, not just a .mp3 suffix.
        if (bytes.Length >= 10 && bytes[..3].SequenceEqual("ID3"u8) && bytes[3] is >= 2 and <= 4
            && (bytes[6] | bytes[7] | bytes[8] | bytes[9]) < 0x80)
            return "audio/mpeg";
        if (bytes.Length >= 4 && bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0
            && (bytes[1] & 0x18) != 0x08 && (bytes[1] & 0x06) != 0
            && (bytes[2] & 0xf0) is not (0 or 0xf0) && (bytes[2] & 0x0c) != 0x0c)
            return "audio/mpeg";
        if (bytes.Length >= 7 && bytes[0] == 0xff && (bytes[1] & 0xf6) == 0xf0
            && ((bytes[2] >> 2) & 0x0f) < 13)
            return "audio/aac";
        return null;
    }
}


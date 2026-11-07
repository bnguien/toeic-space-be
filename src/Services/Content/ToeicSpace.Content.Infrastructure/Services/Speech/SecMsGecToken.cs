using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace ToeicSpace.Content.Infrastructure.Services.Speech;

/// <summary>
/// The <c>Sec-MS-GEC</c> token every request to Microsoft Edge's read-aloud service carries (port of edge-tts 7.3.1
/// <c>drm.generate_sec_ms_gec_v2</c>): the current time as Windows file time rounded down to 5 minutes, followed by
/// the trusted client token, hashed into <c>&lt;16 hex CS64&gt;-&lt;64 hex SHA-256&gt;</c>. The SHA-256 runs from a
/// precomputed mid-state (a hidden 57-byte prefix), so it is not the plain SHA-256 of the string. All arithmetic is
/// modulo 2³²: edge-tts uses Python integers and masks the low 32 bits, which unsigned wrap-around reproduces.
/// </summary>
public static class SecMsGecToken
{
    public const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const long WindowsEpochSeconds = 11_644_473_600;

    private static readonly uint[] Sha256Initial =
        [0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A, 0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19];

    private static readonly uint[] Sha256State =
        [0xFD9FBEA2, 0x91243CE7, 0xBE499094, 0xCD2E1E0A, 0x6C006F49, 0x04BD35AB, 0xFEB0A7E4, 0x03EFFBD2];

    private static readonly uint[] Sha256Schedule =
    [
        0xEB24BF9E, 0x1558691F, 0xA0C09EE2, 0x0CFA32BA, 0xA622EE8C, 0xD1DD9491, 0xC0DD25A0, 0xF9E70AB6, 0xF0C31573,
        0x7058CE4E, 0x7188E606, 0x00E5AB33, 0xD258B060, 0x72766564,
    ];

    private static readonly uint[] RoundConstants =
    [
        0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5, 0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5, 0xD807AA98,
        0x12835B01, 0x243185BE, 0x550C7DC3, 0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174, 0xE49B69C1, 0xEFBE4786,
        0x0FC19DC6, 0x240CA1CC, 0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA, 0x983E5152, 0xA831C66D, 0xB00327C8,
        0xBF597FC7, 0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967, 0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13,
        0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85, 0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3, 0xD192E819,
        0xD6990624, 0xF40E3585, 0x106AA070, 0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5, 0x391C0CB3, 0x4ED8AA4A,
        0x5B9CCA4F, 0x682E6FF3, 0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208, 0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7,
        0xC67178F2,
    ];

    private const uint Sha256Adjustment = 0xC9DC06A7;
    private const uint Sigma0Adjustment = 0x059C0B80;
    private const uint Sigma1Adjustment = 0x000B92C0;

    private static readonly (uint Coefficient, uint Offset)[] Cs64Coefficients =
    [
        (0xE82E91B1, 0xB32700D0), (0x44DE24C6, 0x88DFF51D), (0x3BEB65BC, 0x29514038), (0xC89C5933, 0xB328CBDF),
        (0x8D1EC211, 0xD405B4D5), (0x314EF5D5, 0x26E55F3C), (0xE92491B1, 0x607801C6), (0xF0A5B9D5, 0x82B2C1FB),
        (0x06064CF3, 0xFA51A3DA), (0xDA79FA60, 0x207CC464), (0xBED20560, 0x3C3C74F3), (0x89F9C9F8, 0xD2D14B61),
        (0x35FA38C6, 0xBB04A2C1), (0xCB49E3C6, 0xC5626167),
    ];

    private const uint Cs64Factor = 0x82893933;
    private const uint Cs64Adjustment0 = 0xF2F90B1A;
    private const uint Cs64Adjustment1 = 0x0B1A0000;

    /// <summary>Token for a Unix time in seconds (already corrected for clock skew).</summary>
    public static string Generate(double unixSeconds)
    {
        var seconds = (long)Math.Floor(unixSeconds) + WindowsEpochSeconds;
        seconds -= seconds % 300;
        var ticks = seconds * 10_000_000;
        var data = Encoding.ASCII.GetBytes(ticks.ToString(CultureInfo.InvariantCulture) + TrustedClientToken);
        var digest = Sha256Digest(data);
        var (low, high) = Cs64Digest(data, digest);
        var cs64 = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(cs64, low);
        BinaryPrimitives.WriteUInt32LittleEndian(cs64.AsSpan(4), high);
        return $"{Convert.ToHexString(cs64)}-{Convert.ToHexString(digest)}";
    }

    private static uint Rol16(uint value) => (value << 16) | (value >> 16);
    private static uint Rotr(uint value, int bits) => (value >> bits) | (value << (32 - bits));
    private static uint SmallSigma0(uint v) => Rotr(v, 7) ^ Rotr(v, 18) ^ (v >> 3);
    private static uint SmallSigma1(uint v) => Rotr(v, 17) ^ Rotr(v, 19) ^ (v >> 10);

    private static void Rounds(uint[] state, uint[] words, int firstRound, uint[] constants)
    {
        uint a = state[0], b = state[1], c = state[2], d = state[3], e = state[4], f = state[5], g = state[6], h = state[7];
        for (var i = firstRound; i < 64; i++)
        {
            var t1 = h + (Rotr(e, 6) ^ Rotr(e, 11) ^ Rotr(e, 25)) + (g ^ (e & (f ^ g))) + constants[i] + words[i];
            var t2 = (Rotr(a, 2) ^ Rotr(a, 13) ^ Rotr(a, 22)) + ((a & b) | (c & (a | b)));
            h = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
        }
        state[0] = a; state[1] = b; state[2] = c; state[3] = d; state[4] = e; state[5] = f; state[6] = g; state[7] = h;
    }

    private static void Expand(uint[] words, int firstWord)
    {
        for (var i = firstWord; i < 64; i++)
            words[i] = SmallSigma1(words[i - 2]) + words[i - 7] + SmallSigma0(words[i - 15]) + words[i - 16];
    }

    /// <summary>State after the hidden prefix and the first 7 bytes of the data.</summary>
    private static uint[] InitialState(ReadOnlySpan<byte> first7)
    {
        var w = new uint[64];
        w[14] = (uint)((first7[0] << 16) | (first7[1] << 8) | first7[2]);
        w[15] = BinaryPrimitives.ReadUInt32BigEndian(first7[3..7]);
        for (var i = 16; i < 30; i++)
        {
            var value = Sha256Schedule[i - 16];
            if (i - 2 >= 14) value += i == 16 ? SmallSigma1(w[i - 2]) ^ Sigma1Adjustment : SmallSigma1(w[i - 2]);
            if (i - 7 >= 14) value += w[i - 7];
            if (i - 15 >= 14) value += i == 29 ? SmallSigma0(w[i - 15]) ^ Sigma0Adjustment : SmallSigma0(w[i - 15]);
            w[i] = value;
        }
        w[30] = SmallSigma1(w[28]) + w[23] + SmallSigma0(w[15]) + w[14] + Sha256Adjustment - RoundConstants[14];
        Expand(w, 31);
        var blockConstants = (uint[])RoundConstants.Clone();
        blockConstants[14] = Sha256Adjustment;
        var working = (uint[])Sha256State.Clone();
        Rounds(working, w, 14, blockConstants);
        var state = new uint[8];
        for (var i = 0; i < 8; i++) state[i] = Sha256Initial[i] + working[i];
        return state;
    }

    internal static byte[] Sha256Digest(byte[] data)
    {
        if (data.Length < 7) throw new ArgumentException("Token salt must contain at least seven bytes", nameof(data));
        var length = 57 + data.Length;
        var zeros = ((55 - length) % 64 + 64) % 64;
        var padded = new byte[data.Length - 7 + 1 + zeros + 8];
        data.AsSpan(7).CopyTo(padded);
        padded[data.Length - 7] = 0x80;
        BinaryPrimitives.WriteUInt64BigEndian(padded.AsSpan(padded.Length - 8), (ulong)length * 8);
        var state = InitialState(data.AsSpan(0, 7));
        var words = new uint[64];
        for (var offset = 0; offset < padded.Length; offset += 64)
        {
            for (var i = 0; i < 16; i++) words[i] = BinaryPrimitives.ReadUInt32BigEndian(padded.AsSpan(offset + i * 4, 4));
            Expand(words, 16);
            var working = (uint[])state.Clone();
            Rounds(working, words, 0, RoundConstants);
            for (var i = 0; i < 8; i++) state[i] += working[i];
        }
        var digest = new byte[32];
        for (var i = 0; i < 8; i++) BinaryPrimitives.WriteUInt32BigEndian(digest.AsSpan(i * 4), state[i]);
        return digest;
    }

    private static (uint, uint) Cs64Digest(byte[] data, byte[] digest)
    {
        var keyA = BinaryPrimitives.ReadUInt32LittleEndian(digest) | 1;
        var keyB = BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(4)) | 1;
        uint[] factors = [keyA * Cs64Factor, keyB * Cs64Factor];
        var terms = new List<uint>();
        var offsets = new List<uint>();
        for (var i = 0; i < Cs64Coefficients.Length; i++)
        {
            terms.Add(Cs64Coefficients[i].Coefficient * factors[i % 2]);
            offsets.Add(Cs64Coefficients[i].Offset);
        }
        // Little-endian words of 0x00 ‖ data, zero padded, even count.
        var bytes = new byte[1 + data.Length + ((4 - (1 + data.Length) % 4) % 4)];
        data.CopyTo(bytes, 1);
        var wordCount = bytes.Length / 4;
        for (var index = 0; index < wordCount + (wordCount % 2); index++)
        {
            var word = index < wordCount ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4, 4)) : 0u;
            var term = word * (index % 2 == 0 ? keyA : keyB);
            var offset = Rol16(word) * (index % 2 == 0 ? 0xEF0569FBu : 0xC31713DBu);
            if (index == 0)
            {
                term += Cs64Adjustment0 * factors[0];
                offset += Cs64Adjustment1;
            }
            terms.Add(term);
            offsets.Add(offset);
        }
        uint swapAcc = 0, swapLast = 0, state = 0, acc = 0;
        for (var i = 0; i < terms.Count; i += 2)
        {
            uint x = terms[i], y = terms[i + 1];
            var t = Rol16(offsets[i] + x);
            var q = t * 0x76F50F34;
            swapAcc += Rol16(q) * 0xC3EFEA97 + q;
            t = Rol16(offsets[i + 1] + y);
            q = t * 0x1881CE3C;
            swapLast = Rol16(q) * 0x35BD1EC9 + q;
            swapAcc += swapLast;

            state = state * keyA + x;
            state = Rol16(state) * 0xCF98B111;
            state = Rol16(state) * 0x87085B9F;
            state = Rol16(state) * 0x12CEB96D;
            state = Rol16(state) * 0x257E1D83;
            acc += state;
            t = state * keyB + y;
            t = Rol16(t) * 0xA27416F5;
            t = Rol16(t) * 0xD38396FF;
            state = Rol16(Rol16(t) * 0x7C932B89) * 0xBFA49F69;
            acc += state;
        }
        return (swapLast ^ state, swapAcc ^ acc);
    }
}

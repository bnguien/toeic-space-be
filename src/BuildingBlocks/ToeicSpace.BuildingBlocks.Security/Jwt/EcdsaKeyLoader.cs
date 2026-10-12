using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ToeicSpace.BuildingBlocks.Security.Jwt;

/// <summary>
/// Loads the P-256 keys used for ES256 access tokens.
/// A key can be configured as PEM or as single-line base64 DER
/// (PKCS#8 for the private key, SubjectPublicKeyInfo for the public key),
/// which is easier to pass through environment variables.
/// </summary>
public static class EcdsaKeyLoader
{
    private const string P256Oid = "1.2.840.10045.3.1.7";

    public static ECDsa LoadPrivateKey(string? value, string settingName)
    {
        var key = Import(value, settingName, (ecdsa, der) => ecdsa.ImportPkcs8PrivateKey(der, out _));

        try
        {
            // Throws when the value only contains a public key.
            key.ExportParameters(includePrivateParameters: true);
        }
        catch (CryptographicException exception)
        {
            key.Dispose();
            throw Invalid(settingName, "it does not contain a private key", exception);
        }

        return key;
    }

    public static ECDsa LoadPublicKey(string? value, string settingName)
    {
        if (value?.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Services that only validate tokens must never receive the signing key.
            throw Invalid(settingName, "it contains a private key; configure only the public key here");
        }

        return Import(value, settingName, (ecdsa, der) => ecdsa.ImportSubjectPublicKeyInfo(der, out _));
    }

    public static ECDsa ToPublicKey(ECDsa key)
        => ECDsa.Create(key.ExportParameters(includePrivateParameters: false));

    /// <summary>
    /// RFC 7638 thumbprint of the public key, used as the JWT "kid".
    /// </summary>
    public static string ComputeKeyId(ECDsa key)
    {
        using var publicKey = ToPublicKey(key);
        var jsonWebKey = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(new ECDsaSecurityKey(publicKey));

        return Base64UrlEncoder.Encode(jsonWebKey.ComputeJwkThumbprint());
    }

    private static ECDsa Import(string? value, string settingName, Action<ECDsa, byte[]> importDer)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"'{settingName}' is not configured. Run 'dotnet run scripts/GenerateJwtKeys.cs' " +
                "to create a development key pair, or set it through an environment variable.");
        }

        var key = ECDsa.Create();

        try
        {
            if (value.Contains("-----BEGIN", StringComparison.Ordinal))
            {
                key.ImportFromPem(value);
            }
            else
            {
                importDer(key, Convert.FromBase64String(value.Trim()));
            }

            if (key.ExportParameters(includePrivateParameters: false).Curve.Oid.Value != P256Oid)
            {
                throw new CryptographicException("The key does not use the P-256 curve.");
            }

            return key;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            key.Dispose();
            throw Invalid(settingName, "it is not a valid P-256 key", exception);
        }
    }

    private static InvalidOperationException Invalid(string settingName, string reason, Exception? inner = null)
        => new($"'{settingName}' is invalid: {reason}.", inner);
}

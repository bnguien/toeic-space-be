// Generates the ES256 (P-256) key pair used to sign and validate access tokens.
//
//   dotnet run scripts/GenerateJwtKeys.cs
//       Prints JWT_PRIVATE_KEY / JWT_PUBLIC_KEY lines for docker-compose's .env.
//
//   dotnet run scripts/GenerateJwtKeys.cs -- --user-secrets
//       Stores a new pair in the local user-secrets of the Identity service (private key)
//       and the Assessment service (public key) for `dotnet run`.
//
// Only the Identity service may ever receive the private key. Rotating the pair signs
// everyone out, because existing access tokens no longer validate.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string IdentitySecretsId = "toeicspace-identity-local";
const string AssessmentSecretsId = "toeicspace-assessment-local";

using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var privateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

if (args.Contains("--user-secrets"))
{
    SetSecret(IdentitySecretsId, "Jwt:PrivateKey", privateKey);
    SetSecret(AssessmentSecretsId, "Jwt:PublicKey", publicKey);

    Console.WriteLine("Stored Jwt:PrivateKey for Identity and Jwt:PublicKey for Assessment in user-secrets.");
    Console.WriteLine("Restart both services. Existing sessions are no longer valid.");
    return;
}

Console.WriteLine("# Keep JWT_PRIVATE_KEY secret: only the Identity service needs it.");
Console.WriteLine($"JWT_PRIVATE_KEY={privateKey}");
Console.WriteLine($"JWT_PUBLIC_KEY={publicKey}");

// The value goes through stdin so the private key never appears in a process command line.
static void SetSecret(string secretsId, string name, string value)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    foreach (var argument in new[] { "user-secrets", "set", "--id", secretsId })
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start 'dotnet user-secrets'.");

    // File-based apps disable reflection-based JSON, so the payload is written directly.
    var payload = new MemoryStream();
    using (var writer = new Utf8JsonWriter(payload))
    {
        writer.WriteStartObject();
        writer.WriteString(name, value);
        writer.WriteEndObject();
    }

    process.StandardInput.Write(Encoding.UTF8.GetString(payload.ToArray()));
    process.StandardInput.Close();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"'dotnet user-secrets set' failed for {secretsId}: {process.StandardError.ReadToEnd()}");
    }
}

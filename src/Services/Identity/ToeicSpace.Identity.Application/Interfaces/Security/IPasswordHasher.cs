namespace ToeicSpace.Identity.Application.Interfaces.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Compares in constant time. A null or malformed hash still runs a full key derivation
    /// and returns false, so unknown accounts take as long as wrong passwords.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}

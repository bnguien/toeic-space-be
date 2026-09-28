namespace ToeicSpace.Identity.Application.Interfaces.Security;

public interface ICookieService
{
    /// <summary>
    /// Writes an HttpOnly, Secure, SameSite=Strict cookie scoped to <paramref name="path"/>.
    /// </summary>
    void Set(
        string name,
        string value,
        DateTimeOffset expiresAt,
        string path);

    string? Get(string name);

    void Delete(string name, string path);
}

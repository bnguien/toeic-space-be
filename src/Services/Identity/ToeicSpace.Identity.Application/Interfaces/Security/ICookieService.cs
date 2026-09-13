namespace ToeicSpace.Identity.Application.Interfaces.Security;

public interface ICookieService
{
    void Set(
        string name,
        string value,
        DateTimeOffset expiresAt);

    string? Get(string name);

    void Delete(string name);
}

namespace ToeicSpace.Identity.Application.Interfaces.Security;

public interface ITemporaryPasswordGenerator
{
    /// <summary>
    /// Creates a random password that satisfies the registration password rules.
    /// </summary>
    string Generate();
}

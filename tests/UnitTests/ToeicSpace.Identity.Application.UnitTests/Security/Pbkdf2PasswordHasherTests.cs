using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Application.UnitTests.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verifies_only_the_original_password()
    {
        var hash = _hasher.Hash("Correct-Horse-42");

        _hasher.Verify("Correct-Horse-42", hash).Should().BeTrue();
        _hasher.Verify("correct-horse-42", hash).Should().BeFalse();
        _hasher.Verify("", hash).Should().BeFalse();
    }

    [Fact]
    public void Same_password_gets_a_different_salt_every_time()
    {
        _hasher.Hash("Correct-Horse-42").Should().NotBe(_hasher.Hash("Correct-Horse-42"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain-text-password")]
    [InlineData("PBKDF2-SHA512$abc$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA512$999999999$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("MD5$1$AAAA$AAAA")]
    public void Missing_or_malformed_hashes_never_match(string? storedHash)
    {
        _hasher.Verify("plain-text-password", storedHash).Should().BeFalse();
    }
}

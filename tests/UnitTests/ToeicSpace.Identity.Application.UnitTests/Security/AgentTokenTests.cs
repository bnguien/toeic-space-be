using Microsoft.IdentityModel.JsonWebTokens;
using ToeicSpace.Identity.API.Assistant;
using ToeicSpace.Identity.Application.UnitTests.Support;

namespace ToeicSpace.Identity.Application.UnitTests.Security;

/// <summary>Assistant tokens: read-only, and the on-behalf header is only an audit claim.</summary>
public sealed class AgentTokenTests
{
    private static readonly Guid AgentSubject = Guid.NewGuid();

    [Fact]
    public async Task OnBehalfValue_IsWrittenToTheActClaim_WithoutWideningTheToken()
    {
        await using var context = await IdentityTestContext.CreateAsync();

        var token = new JsonWebToken(context.AccessTokenIssuer
            .IssueForAgent(AgentSubject, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10), "admin:42")
            .Token);

        token.GetClaim("act").Value.Should().Be("admin:42");
        token.GetClaim("scope").Value.Should().Be("agent.read");
        token.GetClaim("role").Value.Should().Be("Admin");
        token.GetClaim("sub").Value.Should().Be(AgentSubject.ToString());
    }

    [Fact]
    public async Task WithoutOnBehalf_ThereIsNoActClaim()
    {
        await using var context = await IdentityTestContext.CreateAsync();

        var token = new JsonWebToken(context.AccessTokenIssuer
            .IssueForAgent(AgentSubject, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10))
            .Token);

        token.TryGetClaim("act", out _).Should().BeFalse();
        token.GetClaim("scope").Value.Should().Be("agent.read");
    }

    [Theory]
    [InlineData("admin:3f2a", null, "admin:3f2a")]
    [InlineData("u_1-2", "admin", "u_1-2")]
    [InlineData("u_1-2", "Admin", "u_1-2")]
    public void ValidHeaders_AreAccepted(string onBehalfOf, string? role, string expected)
    {
        AgentOnBehalf.TryParse(onBehalfOf, role, out var actor, out var error).Should().BeTrue();
        actor.Should().Be(expected);
        error.Should().BeNull();
    }

    [Fact]
    public void NoHeaders_MeanNoActor()
    {
        AgentOnBehalf.TryParse(null, null, out var actor, out _).Should().BeTrue();
        actor.Should().BeNull();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("has space", null)]
    [InlineData("mail@example.com", null)]
    [InlineData("x\"}", null)]
    [InlineData("ok", "teacher")]
    [InlineData("ok", "user")]
    [InlineData(null, "admin")]
    public void MalformedHeaders_AreRejected(string? onBehalfOf, string? role)
    {
        AgentOnBehalf.TryParse(onBehalfOf, role, out var actor, out var error).Should().BeFalse();
        actor.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValuesLongerThan64Characters_AreRejected()
    {
        AgentOnBehalf.TryParse(new string('a', 64), null, out _, out _).Should().BeTrue();
        AgentOnBehalf.TryParse(new string('a', 65), null, out _, out _).Should().BeFalse();
    }
}

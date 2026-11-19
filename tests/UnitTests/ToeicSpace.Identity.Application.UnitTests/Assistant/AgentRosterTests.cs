using System.Text.Json;
using ToeicSpace.Identity.API.Assistant;

namespace ToeicSpace.Identity.Application.UnitTests.Assistant;

/// <summary>The admin panel sees active, non-core agents without internal IDs, prompts or scores.</summary>
public sealed class AgentRosterTests
{
    private const string Roster = """
        [
          { "id": "a1", "orgId": "org-9", "code": "analyst", "name": "Data Analyst", "kind": "specialist",
            "role": "analyst", "capabilities": ["analytics.query", "chart"], "score": 0.92, "status": "active",
            "persona": "Calm and precise", "instructions": "SECRET PROMPT", "uses": 42, "wins": 30 },
          { "id": "a2", "orgId": "org-9", "code": "ceo", "name": "CEO", "kind": "lead", "role": "core",
            "capabilities": [], "score": 1, "status": "active", "persona": null, "instructions": "x", "uses": 9, "wins": 9 },
          { "id": "a3", "orgId": "org-9", "code": "writer", "name": "Writer", "kind": "specialist", "role": "content",
            "capabilities": ["write"], "score": 0.5, "status": "paused", "persona": "Fun", "instructions": "y", "uses": 1, "wins": 0 }
        ]
        """;

    [Fact]
    public void KeepsActiveNonCoreAgents_AndStripsInternalFields()
    {
        var agents = AgentRoster.Parse(Roster);

        agents.Should().NotBeNull();
        var agent = agents!.Should().ContainSingle().Subject;
        agent.Code.Should().Be("analyst");
        agent.Name.Should().Be("Data Analyst");
        agent.Kind.Should().Be("specialist");
        agent.Role.Should().Be("analyst");
        agent.Capabilities.Should().Equal("analytics.query", "chart");
        agent.Status.Should().Be("active");
        agent.Persona.Should().Be("Calm and precise");
        agent.Uses.Should().Be(42);
        agent.Wins.Should().Be(30);

        var json = JsonSerializer.Serialize(agents, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().NotContain("SECRET").And.NotContain("org-9").And.NotContain("\"a1\"")
            .And.NotContain("orgId").And.NotContain("instructions").And.NotContain("score");
        json.Should().Be("""[{"code":"analyst","name":"Data Analyst","kind":"specialist","role":"analyst","capabilities":["analytics.query","chart"],"status":"active","persona":"Calm and precise","uses":42,"wins":30}]""");
    }

    [Fact]
    public void AcceptsAWrappedList()
    {
        AgentRoster.Parse("{\"agents\":" + Roster + "}").Should().ContainSingle();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"error\":\"nope\"}")]
    [InlineData("42")]
    public void RejectsAnythingElse(string body)
    {
        AgentRoster.Parse(body).Should().BeNull();
    }
}

using System.Text.Json.Nodes;

namespace ToeicSpace.Content.Application.Features.GrammarContent.Queries.GetGrammarStageTest;

/// <summary>A stage test ("01".."05") without answers + the viewer's status (guests: <c>unlocked = false</c>).</summary>
public sealed record GetGrammarStageTestQuery(string Stage, Guid? LearnerId) : IRequest<JsonObject>;

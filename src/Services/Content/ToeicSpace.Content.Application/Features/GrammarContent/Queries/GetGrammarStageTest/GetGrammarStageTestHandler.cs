using System.Text.Json;
using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarContent.Queries.GetGrammarStageTest;

public sealed class GetGrammarStageTestHandler(GrammarContentProvider content, GrammarProgressService progress)
    : IRequestHandler<GetGrammarStageTestQuery, JsonObject>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public async Task<JsonObject> Handle(GetGrammarStageTestQuery request, CancellationToken ct)
    {
        var stage = await content.GetStageAsync(request.Stage, ct);
        var status = await progress.StageStatusAsync(request.LearnerId, stage, ct);
        var result = (JsonObject)stage.PublicTest.DeepClone();
        result["status"] = JsonSerializer.SerializeToNode(status, Web);
        return result;
    }
}

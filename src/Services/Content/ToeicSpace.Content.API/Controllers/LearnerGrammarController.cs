using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Features.GrammarProgress.Commands.CompleteTopicPart;
using ToeicSpace.Content.Application.Features.GrammarProgress.Commands.SaveStudyPosition;
using ToeicSpace.Content.Application.Features.GrammarProgress.Commands.SetKnownWord;
using ToeicSpace.Content.Application.Features.GrammarProgress.Commands.UpdateGrammarPreferences;
using ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarMistakes;
using ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarPreferences;
using ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarProgress;
using ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarTraps;
using ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetKnownWords;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>The signed-in learner's grammar progress and state (any role); the learner is always the token subject.</summary>
[ApiController]
[Route("api/v1/grammar/me")]
[Authorize]
public sealed class LearnerGrammarController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet("progress")]
    public Task<GrammarProgressDto> Progress(CancellationToken ct) => sender.Send(new GetGrammarProgressQuery(LearnerId), ct);

    [HttpPost("topics/{topic:regex(^\\d{{2}}$)}/complete")]
    public Task<CompletePartResultDto> Complete(string topic, [FromBody] CompleteTopicPartCommand command, CancellationToken ct) =>
        sender.Send(command with { Topic = topic, LearnerId = LearnerId }, ct);

    [HttpPut("position")]
    public Task<PositionResultDto> Position([FromBody] SaveStudyPositionCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);

    [HttpGet("known-words")]
    public Task<IReadOnlyList<string>> KnownWords([FromQuery] string topic, CancellationToken ct) =>
        sender.Send(new GetKnownWordsQuery(LearnerId, topic ?? string.Empty), ct);

    [HttpPut("known-words")]
    public Task<KnownWordResultDto> SetKnownWord([FromBody] SetKnownWordCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);

    [HttpGet("mistakes")]
    public Task<IReadOnlyList<MistakeGroupDto>> Mistakes(CancellationToken ct) =>
        sender.Send(new GetGrammarMistakesQuery(LearnerId), ct);

    [HttpGet("traps")]
    public Task<IReadOnlyList<TrapDto>> Traps(CancellationToken ct) => sender.Send(new GetGrammarTrapsQuery(LearnerId), ct);

    [HttpGet("preferences")]
    public Task<GrammarPreferencesDto> Preferences(CancellationToken ct) =>
        sender.Send(new GetGrammarPreferencesQuery(LearnerId), ct);

    [HttpPut("preferences")]
    public Task<GrammarPreferencesDto> UpdatePreferences([FromBody] UpdateGrammarPreferencesCommand command,
        CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);
}

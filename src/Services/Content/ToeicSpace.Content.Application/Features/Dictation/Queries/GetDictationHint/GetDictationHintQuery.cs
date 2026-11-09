using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationHint;

/// <summary>Body of <c>POST hint</c>: reveals one letter or one word at the first wrong/missing position.</summary>
public sealed record GetDictationHintQuery(int ItemId, string? UserText, DictationHintMode? Mode)
    : IRequest<DictationHintDto>;

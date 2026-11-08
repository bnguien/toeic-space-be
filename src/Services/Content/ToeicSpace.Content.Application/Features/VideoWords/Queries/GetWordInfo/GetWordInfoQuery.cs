using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GetWordInfo;

/// <summary>Word card details of one word (public).</summary>
public sealed record GetWordInfoQuery(string? Word) : IRequest<WordInfoDto>;

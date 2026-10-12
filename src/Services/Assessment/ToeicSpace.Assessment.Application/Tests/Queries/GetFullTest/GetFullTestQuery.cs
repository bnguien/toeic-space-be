using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Tests.Queries.GetFullTest;

/// <param name="IncludeAnswers">Answer key, explanations and transcripts. Content managers only.</param>
public sealed record GetFullTestQuery(
    Guid Id,
    bool IncludeAnswers = false) : IRequest<FullTestDto>;

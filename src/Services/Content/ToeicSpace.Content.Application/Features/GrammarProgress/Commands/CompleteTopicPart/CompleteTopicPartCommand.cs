using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.CompleteTopicPart;

/// <summary>"Tôi đã hiểu bài" (<c>theory</c>) / "Đã học qua các từ" (<c>vocab</c>). Idempotent.</summary>
public sealed record CompleteTopicPartCommand(string Part) : IRequest<CompletePartResultDto>
{
    public const string Theory = "theory";
    public const string Vocab = "vocab";
    public string Topic { get; init; } = string.Empty;
    public Guid LearnerId { get; init; }
}

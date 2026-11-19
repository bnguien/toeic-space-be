using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Queries.GetComments;

/// <summary>Root comments newest first (20 per page, older with <c>before</c>), each with its first 3 replies.</summary>
public sealed record GetCommentsQuery(string Target, long? Before, Guid? ViewerId) : IRequest<CommentPageDto>;

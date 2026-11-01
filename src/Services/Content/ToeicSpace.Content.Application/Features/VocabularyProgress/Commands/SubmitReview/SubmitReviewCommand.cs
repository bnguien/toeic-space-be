using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;

/// <summary>Body of <c>POST me/reviews</c>; the learner id comes from the access token.</summary>
public sealed record SubmitReviewCommand(Guid WordId, StudyMode Mode, ReviewGrade Grade, Guid ClientEventId,
    int TzOffset = 0, [property: JsonIgnore] Guid LearnerId = default) : IRequest<ReviewResultDto>;

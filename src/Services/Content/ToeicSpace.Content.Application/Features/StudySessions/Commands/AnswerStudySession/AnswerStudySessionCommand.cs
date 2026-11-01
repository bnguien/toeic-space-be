using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.AnswerStudySession;

/// <summary>Grades the current card: records the review (same rules as me/reviews) and advances the session.</summary>
public sealed record AnswerStudySessionCommand(Guid WordId, ReviewGrade Grade, Guid ClientEventId, int TzOffset = 0,
    [property: JsonIgnore] Guid SessionId = default, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<StudySessionDto>;

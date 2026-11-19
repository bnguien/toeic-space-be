using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetCourseVocab;

public sealed record GetCourseVocabQuery(int CourseId) : IRequest<CourseVocabDto>;

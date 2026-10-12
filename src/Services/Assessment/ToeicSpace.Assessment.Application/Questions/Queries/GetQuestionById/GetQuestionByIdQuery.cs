using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Questions.Queries.GetQuestionById;

public sealed record GetQuestionByIdQuery(Guid Id) : IRequest<QuestionDetailDto>;

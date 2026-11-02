using ToeicSpace.Assessment.Application.DTOs.Questions;


namespace ToeicSpace.Assessment.Application.Features.Questions.Queries.GetQuestionById;

public sealed record GetQuestionByIdQuery(Guid Id) : IRequest<QuestionDetailDto>;

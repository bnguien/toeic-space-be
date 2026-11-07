using System.Text.Json.Nodes;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RevealQuestion;

public sealed record RevealQuestionCommand(string QuestionId, Guid? LearnerId) : IRequest<JsonObject>;

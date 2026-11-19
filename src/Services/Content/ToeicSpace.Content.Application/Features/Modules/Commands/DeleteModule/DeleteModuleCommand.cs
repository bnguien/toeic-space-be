namespace ToeicSpace.Content.Application.Features.Modules.Commands.DeleteModule;

public sealed record DeleteModuleCommand(Guid CourseId, Guid ModuleId) : IRequest;

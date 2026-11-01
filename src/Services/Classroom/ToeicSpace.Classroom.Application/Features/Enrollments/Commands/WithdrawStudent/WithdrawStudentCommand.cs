using ToeicSpace.Classroom.Application.DTOs.Enrollments;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.WithdrawStudent;

public sealed record WithdrawStudentCommand(Guid ClassId, Guid EnrollmentId, Guid AdminId) : IRequest<ClassEnrollmentDto>;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Classroom.Application.DTOs.Attendance;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Services;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using Xunit;
namespace ToeicSpace.Classroom.Tests;
public sealed class AttendanceTests
{
 [Fact] public async Task Partial_attendance_retries_updates_removal_and_authorization()
 {
 await using var db=new ClassroomDbContext(new DbContextOptionsBuilder<ClassroomDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
 var cls=new Class{Code="ATT",Name="Attendance"};var session=new ClassSession{ClassId=cls.Id};var teacher=Guid.NewGuid();var student=Guid.NewGuid();
 db.Classes.Add(cls);db.ClassSessions.Add(session);db.ClassInstructors.Add(new ClassInstructor{ClassId=cls.Id,TeacherId=teacher});
 db.Students.Add(new StudentReadModel{StudentId=student,Name="Student",Role="User",Status="Active",IsEligible=true,IsVerified=true});
 db.ClassEnrollments.Add(new ClassEnrollment{ClassId=cls.Id,StudentId=student});await db.SaveChangesAsync();
 var repository=new AttendanceRepository(db,new StudentEligibilityService(new StudentReadRepository(db),NullLogger<StudentEligibilityService>.Instance),TimeProvider.System);
 AttendanceInputDto[] input=[new(student,AttendanceStatus.Present,null)];
 await repository.WriteAsync(session.Id,teacher,"Teacher",input,null,default);
 await repository.WriteAsync(session.Id,teacher,"Teacher",input,null,default);
 Assert.Single(db.Set<AttendanceRecord>());Assert.Single(db.Set<AttendanceAudit>());
 await repository.WriteAsync(session.Id,teacher,"Teacher",[new(student,AttendanceStatus.Late,"Traffic")],null,default);
 Assert.Equal(2,await db.Set<AttendanceAudit>().CountAsync());
 await Assert.ThrowsAsync<ClassRuleException>(()=>repository.ReadAsync(session.Id,Guid.NewGuid(),"Teacher","records",null,default));
 await Assert.ThrowsAsync<ClassRuleException>(()=>repository.WriteAsync(session.Id,student,"User",input,null,default));
 await repository.WriteAsync(session.Id,teacher,"Teacher",[],student,default);
 Assert.Empty(db.Set<AttendanceRecord>());Assert.Equal(3,await db.Set<AttendanceAudit>().CountAsync());
 session.Status=SessionStatus.Cancelled;await db.SaveChangesAsync();
 await Assert.ThrowsAsync<ClassRuleException>(()=>repository.WriteAsync(session.Id,teacher,"Teacher",input,null,default));
 }
}

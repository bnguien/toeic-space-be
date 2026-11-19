using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class ClassRepositoryTests
{
    [Fact]
    public async Task Repository_and_unit_of_work_share_context_and_queries_hide_soft_deleted_classes()
    {
        var options = new DbContextOptionsBuilder<ClassroomDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ClassroomDbContext(options);
        var repository = new ClassRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var teacherId = Guid.NewGuid();
        var active = new Class { Code = "A-1", Name = "Active", Status = ClassStatus.Active, UpdatedAt = DateTime.UtcNow };
        active.Instructors.Add(new ClassInstructor { TeacherId = teacherId, AssignedAt = DateTime.UtcNow });
        var deleted = new Class { Code = "D-1", Name = "Deleted", Status = ClassStatus.Deleted };

        repository.Add(active);
        repository.Add(deleted);
        await unitOfWork.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.True(await repository.CodeExistsAsync("D-1", default)); // Soft delete retains the code.
        Assert.Null(await repository.GetDetailAsync(deleted.Id, default));
        Assert.NotNull(await repository.GetByIdAsync(deleted.Id, true, default));
        var result = await repository.SearchAsync(new ClassSearchCriteria(null, null, null, null, null), default);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(active.Id, Assert.Single(result.Items).Id);
        var mine = await repository.GetTeacherClassesAsync(teacherId, 1, 20, default);
        Assert.Equal(active.Id, Assert.Single(mine.Items).Id);
        var deletedPage = await repository.GetDeletedAsync("D-", 1, 20, default);
        Assert.Equal(1, deletedPage.TotalCount);
        Assert.Equal(deleted.Id, Assert.Single(deletedPage.Items).Id);
        Assert.Empty((await repository.GetDeletedAsync("missing", 1, 20, default)).Items);
    }

    [Fact]
    public async Task Unit_of_work_translates_concurrency_failure_without_exposing_EF_to_API()
    {
        var options = new DbContextOptionsBuilder<ClassroomDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var first = new ClassroomDbContext(options);
        var entity = new Class { Code = "R-1", Name = "Race" };
        first.Classes.Add(entity);
        await new UnitOfWork(first).SaveChangesAsync();

        await using var second = new ClassroomDbContext(options);
        var stale = await second.Classes.SingleAsync(x => x.Id == entity.Id);
        entity.Touch(Guid.NewGuid(), DateTime.UtcNow);
        await new UnitOfWork(first).SaveChangesAsync();

        stale.Touch(Guid.NewGuid(), DateTime.UtcNow);
        var error = await Assert.ThrowsAsync<ClassRuleException>(() => new UnitOfWork(second).SaveChangesAsync());
        Assert.Equal("concurrency_conflict", error.Code);
        Assert.Equal(409, error.StatusCode);
        Assert.IsType<DbUpdateConcurrencyException>(error.InnerException);
    }
}

using System.Text;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Classroom.Application;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.ImportStudents;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.BulkEnrollStudents;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using ToeicSpace.Classroom.Infrastructure.Services.Students;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class EnrollmentImportTests
{
    private static readonly InMemoryDatabaseRoot Root = new();
    private static readonly Guid AdminId = Guid.NewGuid();

    private static ServiceProvider Services(string database, bool failFirstSave = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ClassroomDbContext>(options => options.UseInMemoryDatabase(database, Root));
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IStudentReadRepository, StudentReadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        if (failFirstSave)
        {
            services.AddSingleton<FailOnceFlag>();
            services.AddScoped<IUnitOfWork>(sp => new FailOnceUnitOfWork(
                new UnitOfWork(sp.GetRequiredService<ClassroomDbContext>()), sp.GetRequiredService<FailOnceFlag>()));
        }
        services.AddScoped<IEnrollmentImportReader, EnrollmentImportReader>();
        services.AddSingleton(TimeProvider.System);
        services.AddApplicationServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Csv_import_handles_valid_duplicate_invalid_and_ineligible_rows_independently()
    {
        await using var provider = Services(Guid.NewGuid().ToString());
        var (classId, studentA, studentB) = await SeedAsync(provider);
        var csv = $"\uFEFFstudentId,Note\n{studentA},\"quoted, value\"\n{studentA},duplicate\nnot-a-guid,bad\n{Guid.NewGuid()},unknown\n{studentB},valid\n,missing\n";
        await using var file = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await using var scope = provider.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IEnrollmentImportReader>().ReadAsync(file, "students.csv", default);
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ImportStudentsCommand(classId, AdminId, rows));

        Assert.Equal(6, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(4, result.FailedCount);
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, result.Rows.Select(x => x.RowNumber));
        Assert.Equal("already_enrolled", result.Rows[1].Code);
        Assert.Equal("invalid_student_id", result.Rows[2].Code);
        Assert.Equal("student_not_synchronized", result.Rows[3].Code);
        Assert.NotNull(result.Rows[4].EnrollmentId); // Succeeds after the previous row failed.
        Assert.Equal("missing_student", result.Rows[5].Code);
        var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
        Assert.Equal(2, await db.ClassEnrollments.CountAsync());
    }

    [Fact]
    public async Task Excel_import_resolves_students_by_email_from_local_projection()
    {
        await using var provider = Services(Guid.NewGuid().ToString());
        var (classId, studentA, _) = await SeedAsync(provider);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Students");
        sheet.Cell(1, 1).Value = "email";
        sheet.Cell(2, 1).Value = "one@example.com";
        sheet.Cell(3, 1).Value = "nobody@example.com";
        sheet.Cell(4, 1).Value = "invalid address";
        await using var file = new MemoryStream();
        workbook.SaveAs(file);
        file.Position = 0;
        await using var scope = provider.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IEnrollmentImportReader>().ReadAsync(file, "students.xlsx", default);
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ImportStudentsCommand(classId, AdminId, rows));
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(studentA, result.Rows[0].StudentId);
        Assert.Equal("student_not_synchronized", result.Rows[1].Code);
        Assert.Equal("invalid_email", result.Rows[2].Code);
    }

    [Fact]
    public async Task Failed_database_save_cannot_poison_subsequent_import_rows()
    {
        await using var provider = Services(Guid.NewGuid().ToString(), failFirstSave: true);
        var (classId, studentA, studentB) = await SeedAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new ImportStudentsCommand(classId, AdminId,
                [new EnrollmentImportRow(2, studentA.ToString(), null), new EnrollmentImportRow(3, studentB.ToString(), null)]));
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("concurrency_conflict", result.Rows[0].Code);
        Assert.NotNull(result.Rows[1].EnrollmentId);
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ClassroomDbContext>().ClassEnrollments.ToListAsync());
    }

    [Fact]
    public async Task Bulk_endpoint_supports_one_or_multiple_ids_with_per_student_results()
    {
        await using var provider = Services(Guid.NewGuid().ToString());
        var (classId, studentA, studentB) = await SeedAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var one = new BulkEnrollStudentsCommand([studentA]) { ClassId = classId, AdminId = AdminId };
        var single = await sender.Send(one);
        Assert.Equal(1, single.TotalCount);
        Assert.Equal(1, single.EnrolledCount);
        Assert.NotNull(Assert.Single(single.Items).EnrollmentId);

        var many = await sender.Send(new BulkEnrollStudentsCommand([studentA, studentB, studentB, Guid.NewGuid()])
            { ClassId = classId, AdminId = AdminId });
        Assert.Equal(4, many.TotalCount);
        Assert.Equal(1, many.EnrolledCount);
        Assert.Equal(3, many.FailedCount);
        Assert.Equal(new[] { 0, 1, 2, 3 }, many.Items.Select(x => x.Index));
        Assert.Equal("already_enrolled", many.Items[0].Code);
        Assert.Equal(studentB, many.Items[1].StudentId);
        Assert.NotNull(many.Items[1].EnrollmentId);
        Assert.Equal("already_enrolled", many.Items[2].Code);
        Assert.Equal("student_not_synchronized", many.Items[3].Code);
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ClassroomDbContext>().ClassEnrollments.CountAsync());
    }

    [Fact]
    public async Task Bulk_input_is_validated_and_archived_class_rejects_entire_request()
    {
        var validator = new BulkEnrollStudentsValidator();
        var classId = Guid.NewGuid();
        Assert.False(validator.Validate(new BulkEnrollStudentsCommand([]) { ClassId = classId, AdminId = AdminId }).IsValid);
        Assert.False(validator.Validate(new BulkEnrollStudentsCommand([Guid.Empty]) { ClassId = classId, AdminId = AdminId }).IsValid);
        Assert.False(validator.Validate(new BulkEnrollStudentsCommand(Enumerable.Repeat(Guid.NewGuid(), 1001).ToArray())
            { ClassId = classId, AdminId = AdminId }).IsValid);

        await using var provider = Services(Guid.NewGuid().ToString());
        var (archivedClassId, studentA, _) = await SeedAsync(provider, ClassStatus.Archived);
        await using var scope = provider.CreateAsyncScope();
        var error = await Assert.ThrowsAsync<ClassRuleException>(() => scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new BulkEnrollStudentsCommand([studentA]) { ClassId = archivedClassId, AdminId = AdminId }));
        Assert.Equal("invalid_state", error.Code);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ClassroomDbContext>().ClassEnrollments.ToListAsync());
    }

    [Theory]
    [InlineData("other.txt", "studentId\n")]
    [InlineData("students.csv", "unknown\nvalue\n")]
    [InlineData("students.csv", "studentId,email\na,b\n")]
    [InlineData("students.csv", "studentId\n")]
    public async Task Invalid_file_or_header_is_rejected(string fileName, string csv)
    {
        await using var file = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var exception = await Assert.ThrowsAsync<ClassRuleException>(() =>
            new EnrollmentImportReader().ReadAsync(file, fileName, default));
        Assert.Equal("invalid_import_file", exception.Code);
    }

    [Fact]
    public async Task Corrupt_excel_file_is_rejected_as_invalid_import_file()
    {
        await using var file = new MemoryStream(Encoding.UTF8.GetBytes("not an Excel workbook"));
        Assert.Equal("invalid_import_file", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            new EnrollmentImportReader().ReadAsync(file, "students.xlsx", default))).Code);
    }

    [Fact]
    public async Task Import_limits_rows_and_rejects_non_modifiable_classes_before_writing()
    {
        var csv = "studentId\n" + string.Concat(Enumerable.Repeat($"{Guid.NewGuid()}\n", 1001));
        await using var file = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        Assert.Equal("invalid_import_file", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            new EnrollmentImportReader().ReadAsync(file, "students.csv", default))).Code);

        await using var provider = Services(Guid.NewGuid().ToString());
        var (classId, studentA, _) = await SeedAsync(provider, ClassStatus.Archived);
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var exception = await Assert.ThrowsAsync<ClassRuleException>(() => sender.Send(
            new ImportStudentsCommand(classId, AdminId, [new EnrollmentImportRow(2, studentA.ToString(), null)])));
        Assert.Equal("invalid_state", exception.Code);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ClassroomDbContext>().ClassEnrollments.ToListAsync());
    }

    private static async Task<(Guid ClassId, Guid StudentA, Guid StudentB)> SeedAsync(
        ServiceProvider provider, ClassStatus status = ClassStatus.Draft)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
        var classroom = new Class { Code = Guid.NewGuid().ToString(), Name = "Import", Status = status };
        var first = new StudentReadModel { StudentId = Guid.NewGuid(), Name = "One", Email = "one@example.com",
            Role = "User", Status = "Active", IsVerified = true, IsEligible = true };
        var second = new StudentReadModel { StudentId = Guid.NewGuid(), Name = "Two", Email = "two@example.com",
            Role = "User", Status = "Active", IsVerified = true, IsEligible = true };
        db.Classes.Add(classroom);
        db.Students.AddRange(first, second);
        await db.SaveChangesAsync();
        return (classroom.Id, first.StudentId, second.StudentId);
    }

    private sealed class FailOnceFlag
    {
        public bool ShouldFail = true;
    }

    private sealed class FailOnceUnitOfWork(IUnitOfWork inner, FailOnceFlag flag) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (flag.ShouldFail)
            {
                flag.ShouldFail = false;
                throw new ClassRuleException("concurrency_conflict", "Simulated database failure.", 409);
            }
            return inner.SaveChangesAsync(ct);
        }

        public Task<IClassroomTransaction> BeginTransactionAsync(CancellationToken ct) => inner.BeginTransactionAsync(ct);
    }
}

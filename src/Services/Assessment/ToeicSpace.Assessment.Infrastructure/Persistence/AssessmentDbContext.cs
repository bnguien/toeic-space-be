using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Domain.Entities;
using ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;

namespace ToeicSpace.Assessment.Infrastructure.Persistence;

public class AssessmentDbContext : DbContext, IAssessmentDbContext
{
    public AssessmentDbContext(DbContextOptions<AssessmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<ToeicTest> ToeicTests => Set<ToeicTest>();

    public DbSet<ToeicPassage> ToeicPassages => Set<ToeicPassage>();

    public DbSet<ToeicQuestion> ToeicQuestions => Set<ToeicQuestion>();

    public DbSet<ToeicPracticeSet> ToeicPracticeSets => Set<ToeicPracticeSet>();

    public DbSet<ToeicPracticeSetItem> ToeicPracticeSetItems => Set<ToeicPracticeSetItem>();

    public DbSet<ToeicAttempt> ToeicAttempts => Set<ToeicAttempt>();

    public DbSet<ToeicAttemptAnswer> ToeicAttemptAnswers => Set<ToeicAttemptAnswer>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssessmentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

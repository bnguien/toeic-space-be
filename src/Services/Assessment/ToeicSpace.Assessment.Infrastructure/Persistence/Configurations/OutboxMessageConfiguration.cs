using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(x => new { x.ProcessedOn, x.OccurredOn });
    }
}

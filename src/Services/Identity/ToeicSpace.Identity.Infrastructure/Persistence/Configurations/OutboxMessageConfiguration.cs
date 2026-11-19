using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Identity.Infrastructure.Messaging.Outbox;

namespace ToeicSpace.Identity.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(500).IsRequired();
        b.Property(x => x.Content).HasColumnType("longtext").IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.HasIndex(x => new { x.ProcessedOn, x.OccurredOn });
    }
}

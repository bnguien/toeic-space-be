using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("Courses", table =>
        {
            table.HasCheckConstraint("CK_Courses_Capacity_Positive", "`Capacity` IS NULL OR `Capacity` > 0");
            table.HasCheckConstraint("CK_Courses_Price_Nonnegative", "`Price` >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.ShortDescription).HasMaxLength(500);
        b.Property(x => x.Description).HasMaxLength(10000);
        b.Property(x => x.ThumbnailKey).HasMaxLength(160);
        b.Property(x => x.ExamType).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.Price).HasPrecision(18, 0).HasDefaultValue(0m).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        foreach (var property in new[] { b.Property(x => x.Skills), b.Property(x => x.Objectives), b.Property(x => x.Prerequisites) })
        {
            property.HasConversion(v => Serialize(v), v => Deserialize(v)).HasColumnType("json").IsRequired();
            property.Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (left, right) => left!.SequenceEqual(right!),
                items => items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                items => items.ToList()));
        }
        b.HasIndex(x => new { x.Status, x.UpdatedAt });
    }

    private static string Serialize(List<string> values) => JsonSerializer.Serialize(values);
    private static List<string> Deserialize(string value) => JsonSerializer.Deserialize<List<string>>(value) ?? [];
}

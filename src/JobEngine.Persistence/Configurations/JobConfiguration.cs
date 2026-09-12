using JobEngine.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobEngine.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .ValueGeneratedOnAdd();

        builder.Property(j => j.Type)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(j => j.PayloadJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(j => j.MaxAttempts)
            .HasDefaultValue(3);

        builder.Property(j => j.ClaimedBy)
            .HasMaxLength(100);

        builder.Property(j => j.LastError)
            .HasColumnType("text");

        builder.Property(j => j.Version)
            .IsConcurrencyToken();

        builder.HasIndex(j => new { j.Status, j.ScheduledAt })
            .HasDatabaseName("ix_jobs_status_scheduled_at");
    }
}
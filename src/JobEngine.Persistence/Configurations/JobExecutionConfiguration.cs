using JobEngine.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobEngine.Persistence.Configurations;

public class JobExecutionConfiguration : IEntityTypeConfiguration<JobExecution>
{
    public void Configure(EntityTypeBuilder<JobExecution> builder)
    {
        builder.ToTable("job_executions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.WorkerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Outcome)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(e => e.Job)
            .WithMany()
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(e => new { e.JobId, e.Generation, e.Attempt })
            .IsUnique()
            .HasDatabaseName("ux_job_executions_job_id_generation_attempt");
    }
}
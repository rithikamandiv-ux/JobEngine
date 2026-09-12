using JobEngine.Core;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Persistence;

public class JobDbContext : DbContext
{
    public JobDbContext(DbContextOptions<JobDbContext> options)
        : base(options)
    {
    }

    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobDbContext).Assembly);
    }
}
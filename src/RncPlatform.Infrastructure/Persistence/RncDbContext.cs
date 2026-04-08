using Microsoft.EntityFrameworkCore;
using RncPlatform.Domain.Entities;
using System.Reflection;

namespace RncPlatform.Infrastructure.Persistence;

public class RncDbContext : DbContext
{
    public RncDbContext(DbContextOptions<RncDbContext> options) : base(options)
    {
    }

    public DbSet<Taxpayer> Taxpayers => Set<Taxpayer>();
    public DbSet<RncSnapshot> RncSnapshots => Set<RncSnapshot>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RncChangeLog> RncChangeLogs => Set<RncChangeLog>();
    public DbSet<SyncJobState> SyncJobStates => Set<SyncJobState>();
    public DbSet<RncStaging> RncStaging => Set<RncStaging>();
    public DbSet<DistributedLock> DistributedLocks => Set<DistributedLock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Configurations;

public class SyncJobStateConfiguration : IEntityTypeConfiguration<SyncJobState>
{
    public void Configure(EntityTypeBuilder<SyncJobState> builder)
    {
        builder.HasKey(x => x.JobName);
        builder.Property(x => x.JobName).HasMaxLength(100);
        builder.Property(x => x.LastStatus).HasMaxLength(50);
        builder.Property(x => x.LastMessage).HasMaxLength(2000);
    }
}

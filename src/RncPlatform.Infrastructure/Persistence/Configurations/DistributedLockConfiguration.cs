using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Configurations;

public class DistributedLockConfiguration : IEntityTypeConfiguration<DistributedLock>
{
    public void Configure(EntityTypeBuilder<DistributedLock> builder)
    {
        builder.HasKey(x => x.Resource);
        builder.Property(x => x.Resource).HasMaxLength(200);
        builder.Property(x => x.LockedBy).HasMaxLength(200);
    }
}

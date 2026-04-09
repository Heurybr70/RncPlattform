using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Configurations;

public class RncChangeLogConfiguration : IEntityTypeConfiguration<RncChangeLog>
{
    public void Configure(EntityTypeBuilder<RncChangeLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Rnc);
        builder.HasIndex(x => x.SnapshotId);
        builder.HasIndex(x => new { x.Rnc, x.DetectedAt });

        builder.Property(x => x.ChangeType).HasMaxLength(50);
    }
}

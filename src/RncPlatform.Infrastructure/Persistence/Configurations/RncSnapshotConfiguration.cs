using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Configurations;

public class RncSnapshotConfiguration : IEntityTypeConfiguration<RncSnapshot>
{
    public void Configure(EntityTypeBuilder<RncSnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.SourceName).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.SourceUrl).HasMaxLength(500);
        builder.Property(x => x.SourceFileName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.FileHash).HasMaxLength(256);
    }
}

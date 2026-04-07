using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Configurations;

public class RncStagingConfiguration : IEntityTypeConfiguration<RncStaging>
{
    public void Configure(EntityTypeBuilder<RncStaging> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ExecutionId, x.Rnc });

        builder.Property(x => x.Rnc).HasMaxLength(20);
        builder.Property(x => x.NombreORazonSocial).HasMaxLength(255);
    }
}

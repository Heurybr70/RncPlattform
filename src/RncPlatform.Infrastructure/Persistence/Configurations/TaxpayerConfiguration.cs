using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Configurations;

public class TaxpayerConfiguration : IEntityTypeConfiguration<Taxpayer>
{
    public void Configure(EntityTypeBuilder<Taxpayer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Rnc).IsUnique();
        builder.HasIndex(x => new { x.NombreORazonSocial, x.Rnc })
            .HasDatabaseName("IX_Taxpayers_Name_Rnc");
        builder.HasIndex(x => new { x.NombreComercial, x.Rnc })
            .HasDatabaseName("IX_Taxpayers_CommercialName_Rnc")
            .HasFilter("[NombreComercial] IS NOT NULL");
        builder.HasIndex(x => x.Estado);
        builder.HasIndex(x => x.IsActiveInLatestSnapshot);

        builder.Property(x => x.Rnc).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Cedula).HasMaxLength(20);
        builder.Property(x => x.NombreORazonSocial).HasMaxLength(255).IsRequired();
        builder.Property(x => x.NombreComercial).HasMaxLength(255);
        builder.Property(x => x.Categoria).HasMaxLength(100);
        builder.Property(x => x.RegimenPago).HasMaxLength(100);
        builder.Property(x => x.Estado).HasMaxLength(50);
        builder.Property(x => x.ActividadEconomica).HasMaxLength(255);
        builder.Property(x => x.FechaConstitucion).HasMaxLength(50);

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

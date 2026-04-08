using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Username)
            .IsUnique();

        builder.Property(x => x.Email)
            .HasMaxLength(150);

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL");

        builder.Property(x => x.PasswordHash)
            .IsRequired();

        builder.Property(x => x.FullName)
            .HasMaxLength(100);
    }
}

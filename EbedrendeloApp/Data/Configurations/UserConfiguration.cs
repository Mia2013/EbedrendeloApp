using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.UserId).IsUnique();
        builder.HasIndex(u => u.UserName).IsUnique();

        builder.Property(u => u.UserName).HasMaxLength(64);
        builder.Property(u => u.KeresztNev).HasMaxLength(128);
        builder.Property(u => u.VezetekNev).HasMaxLength(128);
        builder.Property(u => u.Igazgatosag).HasMaxLength(128);
        builder.Property(u => u.Osztaly).HasMaxLength(128);
        builder.Property(u => u.Rf).HasMaxLength(32);
        builder.Property(u => u.SzervKod).HasMaxLength(32);

        builder.HasOne(u => u.Role)
            .WithMany()
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

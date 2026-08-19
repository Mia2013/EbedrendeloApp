using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class ALaCarteItemConfiguration : IEntityTypeConfiguration<ALaCarteItem>
{
    public void Configure(EntityTypeBuilder<ALaCarteItem> builder)
    {
        builder.Property(i => i.Name).HasMaxLength(128);
    }
}

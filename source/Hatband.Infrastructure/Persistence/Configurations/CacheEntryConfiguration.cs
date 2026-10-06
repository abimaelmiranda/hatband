using Hatband.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class CacheEntryConfiguration : IEntityTypeConfiguration<CacheEntry>
{
    public void Configure(EntityTypeBuilder<CacheEntry> builder)
    {
        builder.ToTable("CacheEntries");
        builder.HasKey(entry => entry.Key);
        builder.Property(entry => entry.Key).IsRequired();
        builder.Property(entry => entry.Value).IsRequired();
        builder.Property(entry => entry.ExpiresAt).IsRequired();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Laundry.Identity.Infrastructure;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.AccountType).HasConversion<string>().HasMaxLength(16);
        builder.Property(u => u.CreatedAtUtc);

        // The default index on the normalized email is not unique.
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
    }
}

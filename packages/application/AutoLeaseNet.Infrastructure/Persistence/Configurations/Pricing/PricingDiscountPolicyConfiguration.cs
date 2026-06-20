using System.Globalization;
using AutoLeaseNet.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PricingDiscountPolicyConfiguration : IEntityTypeConfiguration<PricingDiscountPolicy>
{
    public void Configure(EntityTypeBuilder<PricingDiscountPolicy> builder)
    {
        builder.ToTable("PricingDiscountPolicies", "dbo");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.MaxDiscountPercent).HasPrecision(5, 2).IsRequired();

        var converter = new ValueConverter<List<decimal>, string>(
            v => string.Join(',', v.Select(x => x.ToString(CultureInfo.InvariantCulture))),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<decimal>()
                : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => decimal.Parse(x, CultureInfo.InvariantCulture))
                    .ToList());

        var comparer = new ValueComparer<List<decimal>>(
            (l, r) => l != null && r != null && l.SequenceEqual(r),
            l => l.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode())),
            l => l.ToList());

        builder.Property<List<decimal>>("_allowedPresets")
            .HasColumnName("AllowedPresetsCsv")
            .HasMaxLength(300)
            .HasConversion(converter)
            .Metadata.SetValueComparer(comparer);

        builder.Ignore(x => x.AllowedPresets);

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.TenantId).IsUnique();

        builder.Ignore(x => x.DomainEvents);
    }
}

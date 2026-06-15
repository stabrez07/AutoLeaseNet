using AutoLeaseNet.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations.Sales;

public sealed class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> builder)
    {
        builder.ToTable("QuotationLines", "dbo");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.QuotationId).IsRequired();
        builder.Property(l => l.LineNumber).IsRequired();
        builder.Property(l => l.ItemType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.VehicleSpecRef).HasMaxLength(200);
        builder.Property(l => l.Quantity).IsRequired();
        builder.Property(l => l.UnitPriceSar).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.DiscountPercent).HasPrecision(5, 2).IsRequired();
        builder.Property(l => l.LineTotalSar).HasPrecision(18, 2).IsRequired();

        builder.Property(l => l.CreatedAtUtc).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();
        builder.Property(l => l.RowVersion).IsRowVersion();

        builder.HasIndex(l => l.TenantId);
        builder.HasIndex(l => l.QuotationId);
        builder.HasIndex(l => new { l.QuotationId, l.LineNumber }).IsUnique();

        builder.Ignore(l => l.DomainEvents);
    }
}

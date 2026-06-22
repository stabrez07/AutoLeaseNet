using AutoLeaseNet.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ContractLine"/> — child entity of
/// <see cref="Contract"/>. Each line represents a make/model/year pricing
/// entry within the contract.
/// </summary>
public sealed class ContractLineConfiguration : IEntityTypeConfiguration<ContractLine>
{
    public void Configure(EntityTypeBuilder<ContractLine> builder)
    {
        builder.ToTable("ContractLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.ContractId).IsRequired();
        builder.Property(l => l.Make).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Model).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Year).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500);
        builder.Property(l => l.Quantity).IsRequired();
        builder.Property(l => l.UnitPriceSar).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.LineTotalSar).HasPrecision(18, 2).IsRequired();

        // Audit
        builder.Property(l => l.CreatedAtUtc).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();
        builder.Property(l => l.RowVersion).IsRowVersion();

        // Indexes
        builder.HasIndex(l => new { l.TenantId, l.ContractId });

        builder.Ignore(l => l.DomainEvents);
    }
}

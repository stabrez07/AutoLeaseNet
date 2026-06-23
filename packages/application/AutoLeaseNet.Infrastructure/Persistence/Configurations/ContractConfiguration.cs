using AutoLeaseNet.Domain.Contracts;
using AutoLeaseNet.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Contract"/> aggregate and its
/// <see cref="ContractLine"/> children. A Contract sits between Quotation
/// and Lease Agreement in the business hierarchy:
/// Quote -> Contract -> Lease Agreements.
/// </summary>
public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.ContractNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.CustomerId).IsRequired();
        builder.Property(c => c.QuotationId);

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.ContractTypeCode).IsRequired();
        builder.Property(c => c.StartDate).IsRequired();
        builder.Property(c => c.EndDate).IsRequired();
        builder.Property(c => c.DurationMonths).IsRequired();
        builder.Property(c => c.TotalVehicles);
        builder.Property(c => c.PaymentTermsDays).IsRequired();
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.Property(c => c.CheckedOutVehicles);
        builder.Property(c => c.BaseAmountSar).HasPrecision(18, 2);
        builder.Property(c => c.DiscountPercent).HasPrecision(5, 2);
        builder.Property(c => c.DiscountAmountSar).HasPrecision(18, 2);
        builder.Property(c => c.NetAmountSar).HasPrecision(18, 2);
        builder.Property(c => c.VatPercent).HasPrecision(5, 2);
        builder.Property(c => c.VatAmountSar).HasPrecision(18, 2);
        builder.Property(c => c.TotalAmountSar).HasPrecision(18, 2);
        builder.Property(c => c.MonthlyRentSar).HasPrecision(18, 2);
        builder.Property(c => c.TotalContractValueSar).HasPrecision(18, 2);

        // Audit
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();
        builder.Property(c => c.RowVersion).IsRowVersion();

        // Indexes
        builder.HasIndex(c => new { c.TenantId, c.ContractNumber }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.HasIndex(c => new { c.TenantId, c.CustomerId });

        // FK to Customer
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        // Child collection: ContractLines
        builder.HasMany(c => c.Lines)
            .WithOne()
            .HasForeignKey(l => l.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
    }
}

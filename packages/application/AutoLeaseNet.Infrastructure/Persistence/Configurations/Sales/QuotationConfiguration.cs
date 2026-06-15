using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations.Sales;

public sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations", "dbo");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.TenantId).IsRequired();
        builder.Property(q => q.QuoteNumber).HasMaxLength(50).IsRequired();
        builder.Property(q => q.CustomerId).IsRequired();
        builder.Property(q => q.AccountManagerId).IsRequired();

        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(q => q.ContractType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(q => q.QuoteDate).HasColumnType("date").IsRequired();
        builder.Property(q => q.ValidUntilDate).HasColumnType("date").IsRequired();
        builder.Property(q => q.EstimatedDurationMonths).IsRequired();
        builder.Property(q => q.TermsAndConditionsMd).HasMaxLength(4000);

        builder.Property(q => q.SubTotalSar).HasPrecision(18, 2).IsRequired();
        builder.Property(q => q.DiscountPercent).HasPrecision(5, 2).IsRequired();
        builder.Property(q => q.VatSar).HasPrecision(18, 2).IsRequired();
        builder.Property(q => q.TotalSar).HasPrecision(18, 2).IsRequired();

        builder.Property(q => q.SubmittedAtUtc);
        builder.Property(q => q.ApprovedAtUtc);
        builder.Property(q => q.SentAtUtc);
        builder.Property(q => q.AcceptedAtUtc);
        builder.Property(q => q.ClosedAtUtc);
        builder.Property(q => q.PdfBlobUri).HasMaxLength(500);
        builder.Property(q => q.AcceptedByCustomerSignature).HasMaxLength(4000);

        builder.Property(q => q.CreatedAtUtc).IsRequired();
        builder.Property(q => q.UpdatedAtUtc).IsRequired();
        builder.Property(q => q.RowVersion)
            .IsRowVersion()
            .UsePropertyAccessMode(PropertyAccessMode.Property);

        builder.HasIndex(q => q.TenantId);
        builder.HasIndex(q => new { q.TenantId, q.QuoteNumber }).IsUnique();
        builder.HasIndex(q => new { q.TenantId, q.Status });

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(q => q.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(q => q.Lines)
            .WithOne()
            .HasForeignKey(l => l.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Approvals)
            .WithOne()
            .HasForeignKey(a => a.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(q => q.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(q => q.Approvals).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(q => q.DomainEvents);
    }
}

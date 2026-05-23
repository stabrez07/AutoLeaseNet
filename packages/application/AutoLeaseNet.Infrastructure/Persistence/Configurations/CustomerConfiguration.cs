using AutoLeaseNet.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.Type).HasConversion<int>().IsRequired();
        builder.Property(c => c.Status).HasConversion<int>().IsRequired();
        builder.Property(c => c.PreferredLanguage).HasConversion<int>().IsRequired();

        builder.Property(c => c.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(c => c.DisplayNameAr).HasMaxLength(256);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Mobile).HasMaxLength(32);
        builder.Property(c => c.NationalAddress).HasMaxLength(256);

        builder.Property(c => c.LegalName).HasMaxLength(256);
        builder.Property(c => c.LegalNameAr).HasMaxLength(256);
        builder.Property(c => c.CommercialRegistration).HasMaxLength(64);
        builder.Property(c => c.VatNumber).HasMaxLength(64);
        builder.Property(c => c.BillingAddress).HasMaxLength(512);
        builder.Property(c => c.CreditLimit).HasPrecision(18, 2);
        builder.Property(c => c.CreditCurrency).HasMaxLength(8);

        builder.Property(c => c.PersonNameEn).HasMaxLength(256);
        builder.Property(c => c.PersonNameAr).HasMaxLength(256);
        builder.Property(c => c.PersonIdNumber).HasMaxLength(64);
        builder.Property(c => c.NationalityCode).HasMaxLength(8);
        builder.Property(c => c.KycVerifiedBy).HasMaxLength(128);

        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.HasIndex(c => new { c.TenantId, c.Type });
        builder.HasIndex(c => new { c.TenantId, c.CommercialRegistration })
            .IsUnique()
            .HasFilter("[CommercialRegistration] IS NOT NULL");
        builder.HasIndex(c => new { c.TenantId, c.PersonIdNumber })
            .IsUnique()
            .HasFilter("[PersonIdNumber] IS NOT NULL");

        builder.Ignore(c => c.DomainEvents);
    }
}

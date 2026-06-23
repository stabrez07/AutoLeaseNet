using AutoLeaseNet.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.CustomerId).IsRequired();
        builder.Property(a => a.NatureOfBusiness).HasMaxLength(256);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Customer contact
        builder.Property(a => a.CustomerContactNameEn).HasMaxLength(200).IsRequired();
        builder.Property(a => a.CustomerContactNameAr).HasMaxLength(200);
        builder.Property(a => a.CustomerContactPosition).HasMaxLength(200);
        builder.Property(a => a.CustomerContactMobile).HasMaxLength(32);
        builder.Property(a => a.CustomerContactEmail).HasMaxLength(320);

        // Our account holder
        builder.Property(a => a.AccountHolderNameEn).HasMaxLength(200).IsRequired();
        builder.Property(a => a.AccountHolderNameAr).HasMaxLength(200);
        builder.Property(a => a.AccountHolderPosition).HasMaxLength(200);
        builder.Property(a => a.AccountHolderMobile).HasMaxLength(32);
        builder.Property(a => a.AccountHolderEmail).HasMaxLength(320);

        // Address
        builder.Property(a => a.Street).HasMaxLength(512);
        builder.Property(a => a.City).HasMaxLength(128);
        builder.Property(a => a.Region).HasMaxLength(128);
        builder.Property(a => a.PostalCode).HasMaxLength(16);
        builder.Property(a => a.Country).HasMaxLength(64);

        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => new { a.TenantId, a.CustomerId });

        builder.Ignore(a => a.DomainEvents);
    }
}

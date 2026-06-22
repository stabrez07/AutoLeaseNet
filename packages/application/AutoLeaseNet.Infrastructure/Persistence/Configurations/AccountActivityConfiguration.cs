using AutoLeaseNet.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class AccountActivityConfiguration : IEntityTypeConfiguration<AccountActivity>
{
    public void Configure(EntityTypeBuilder<AccountActivity> builder)
    {
        builder.ToTable("AccountActivities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.CustomerId).IsRequired();
        builder.Property(a => a.ActivityType).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Subject).HasMaxLength(512).IsRequired();
        builder.Property(a => a.Body).HasMaxLength(4000);
        builder.Property(a => a.Direction).HasMaxLength(16);
        builder.Property(a => a.LinkedEntityType).HasMaxLength(64);

        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => new { a.TenantId, a.CustomerId });
        builder.HasIndex(a => new { a.TenantId, a.CustomerId, a.CreatedAtUtc })
            .IsDescending(false, false, true);

        builder.Ignore(a => a.DomainEvents);
    }
}

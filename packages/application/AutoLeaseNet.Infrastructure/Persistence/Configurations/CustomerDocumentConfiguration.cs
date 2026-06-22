using AutoLeaseNet.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class CustomerDocumentConfiguration : IEntityTypeConfiguration<CustomerDocument>
{
    public void Configure(EntityTypeBuilder<CustomerDocument> builder)
    {
        builder.ToTable("CustomerDocuments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.CustomerId).IsRequired();
        builder.Property(d => d.DocType).HasMaxLength(64).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(512).IsRequired();
        builder.Property(d => d.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(1024);

        builder.Property(d => d.CreatedAtUtc).IsRequired();
        builder.Property(d => d.UpdatedAtUtc).IsRequired();
        builder.Property(d => d.RowVersion).IsRowVersion();

        builder.HasIndex(d => new { d.TenantId, d.CustomerId });

        builder.Ignore(d => d.DomainEvents);
    }
}

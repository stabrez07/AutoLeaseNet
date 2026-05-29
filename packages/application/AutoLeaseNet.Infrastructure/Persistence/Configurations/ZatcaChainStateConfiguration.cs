using AutoLeaseNet.Domain.Zatca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="ZatcaChainState"/>. Aggregate-of-one per tenant —
/// <c>TenantId</c> is the primary key (overrides the base <c>Entity.Id</c>) so the
/// "one row per tenant" invariant is enforced by the database, not by application
/// code. <c>Id</c> is still mapped (Entity carries it) but is a non-key uniqueidentifier
/// column with a default value, kept for cross-table consistency.
/// </summary>
public sealed class ZatcaChainStateConfiguration : IEntityTypeConfiguration<ZatcaChainState>
{
    public void Configure(EntityTypeBuilder<ZatcaChainState> builder)
    {
        builder.ToTable("ZatcaChainStates");

        // TenantId IS the primary key — single row per tenant by construction.
        builder.HasKey(z => z.TenantId);
        builder.Property(z => z.TenantId).ValueGeneratedNever();

        builder.Property(z => z.Id).IsRequired();

        // SHA-256 hashes are 64 hex chars or 44 Base64 chars; 128 is generous headroom
        // for any future encoding switch without going to MAX.
        builder.Property(z => z.LastClearedInvoiceHash).HasMaxLength(128);
        builder.Property(z => z.LastClearedAtUtc);

        builder.Property(z => z.CreatedAtUtc).IsRequired();
        builder.Property(z => z.UpdatedAtUtc).IsRequired();
        builder.Property(z => z.RowVersion).IsRowVersion();

        builder.Ignore(z => z.DomainEvents);
    }
}

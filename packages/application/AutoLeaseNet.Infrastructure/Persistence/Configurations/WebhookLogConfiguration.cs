using AutoLeaseNet.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="WebhookLog"/>. Unique index on (<c>Source</c>,
/// <c>ExternalEventId</c>) is the dedup primitive — duplicate Tajeer retries surface as
/// <see cref="DbUpdateException"/> and are translated to 200 OK by the receiver.
/// </summary>
public sealed class WebhookLogConfiguration : IEntityTypeConfiguration<WebhookLog>
{
    public void Configure(EntityTypeBuilder<WebhookLog> builder)
    {
        builder.ToTable("WebhookLogs");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.TenantId).IsRequired();
        builder.Property(w => w.Source).HasMaxLength(32).IsRequired();
        builder.Property(w => w.ExternalEventId).HasMaxLength(128).IsRequired();
        builder.Property(w => w.Category).HasMaxLength(64).IsRequired();
        builder.Property(w => w.EventType).HasMaxLength(128).IsRequired();
        builder.Property(w => w.ReferenceId).HasMaxLength(128);
        // Tajeer payloads are small (under 2 KB observed); cap generously without going to MAX
        // so SQL Server can keep the column in-row.
        builder.Property(w => w.Payload).HasMaxLength(8000).IsRequired();
        builder.Property(w => w.Signature).HasMaxLength(256);
        builder.Property(w => w.ProcessingError).HasMaxLength(2048);
        builder.Property(w => w.ReceivedAtUtc).IsRequired();
        builder.Property(w => w.CreatedAtUtc).IsRequired();
        builder.Property(w => w.UpdatedAtUtc).IsRequired();
        builder.Property(w => w.RowVersion).IsRowVersion();

        builder.HasIndex(w => new { w.Source, w.ExternalEventId })
            .IsUnique();

        // Worker drains "unprocessed" rows; this filtered index keeps the scan tight.
        builder.HasIndex(w => new { w.TenantId, w.ProcessedAtUtc })
            .HasFilter("[ProcessedAtUtc] IS NULL");

        builder.Ignore(w => w.DomainEvents);
    }
}

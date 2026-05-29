using AutoLeaseNet.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("OutboxEvents");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.TenantId).IsRequired();
        builder.Property(o => o.EventType).HasMaxLength(512).IsRequired();
        builder.Property(o => o.PayloadJson).IsRequired();
        builder.Property(o => o.CorrelationId);

        builder.Property(o => o.CreatedAtUtc).IsRequired();
        builder.Property(o => o.UpdatedAtUtc).IsRequired();
        builder.Property(o => o.AvailableAtUtc).IsRequired();
        builder.Property(o => o.ProcessedAtUtc);
        builder.Property(o => o.LastError).HasMaxLength(2000);
        builder.Property(o => o.Attempts).IsRequired();
        builder.Property(o => o.RowVersion).IsRowVersion();

        // Drain hot path: cheap FIFO scan over unprocessed rows ordered by availability.
        builder.HasIndex(o => new { o.ProcessedAtUtc, o.AvailableAtUtc })
            .HasDatabaseName("IX_OutboxEvents_Drain")
            .HasFilter("[ProcessedAtUtc] IS NULL");

        // Operator query: "what events fired for tenant X recently?"
        builder.HasIndex(o => new { o.TenantId, o.EventType, o.CreatedAtUtc });

        builder.Ignore(o => o.DomainEvents);
    }
}

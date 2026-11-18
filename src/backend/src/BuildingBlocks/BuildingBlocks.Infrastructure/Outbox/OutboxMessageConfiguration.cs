using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.Type).HasColumnName("type").IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(m => m.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(m => m.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(m => m.Attempts).HasColumnName("attempts");
        builder.Property(m => m.LastError).HasColumnName("last_error");
        builder.Property(m => m.DeadAtUtc).HasColumnName("dead_at_utc");

        builder.HasIndex(m => m.NextAttemptAtUtc)
            .HasDatabaseName("ix_outbox_messages_pending")
            .HasFilter("processed_at_utc IS NULL AND dead_at_utc IS NULL");
    }
}

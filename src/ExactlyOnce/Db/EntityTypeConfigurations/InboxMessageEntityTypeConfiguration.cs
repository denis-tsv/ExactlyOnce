using ExactlyOnce.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExactlyOnce.Db.EntityTypeConfigurations;

public class InboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.Property(x => x.Headers).HasColumnType("jsonb");
        builder.Property(x => x.IdempotenceKey).HasMaxLength(128);
        builder.Property(x => x.Topic).HasMaxLength(128);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => new {x.Topic, x.Partition, x.Offset});
        builder.HasIndex(x => x.IdempotenceKey).IsUnique();
    }
}
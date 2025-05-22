using ExactlyOnce.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExactlyOnce.Db.EntityTypeConfigurations;

public class ProcessedInboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.HasKey(x => x.IdempotenceKey);
        
        builder.Property(x => x.IdempotenceKey).HasMaxLength(128);
    }
}
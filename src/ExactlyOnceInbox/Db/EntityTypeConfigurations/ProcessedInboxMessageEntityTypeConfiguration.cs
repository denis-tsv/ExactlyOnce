using ExactlyOnceInbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExactlyOnceInbox.Db.EntityTypeConfigurations;

public class ProcessedInboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<ProcessedInboxMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedInboxMessage> builder)
    {
        builder.HasKey(x => x.IdempotenceKey);
        
        builder.Property(x => x.IdempotenceKey).HasMaxLength(128);
    }
}
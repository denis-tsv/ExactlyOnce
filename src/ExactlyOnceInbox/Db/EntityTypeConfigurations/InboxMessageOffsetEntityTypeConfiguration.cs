using ExactlyOnceInbox.Configs;
using ExactlyOnceInbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExactlyOnceInbox.Db.EntityTypeConfigurations;

public class InboxMessageOffsetEntityTypeConfiguration : IEntityTypeConfiguration<InboxMessageOffset>
{
    public void Configure(EntityTypeBuilder<InboxMessageOffset> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Topic).HasMaxLength(128);
        builder.Property(x => x.AvailableAfter).HasDefaultValueSql("now()");

        builder.HasIndex(x => new {x.Topic, x.Partition}).IsUnique();

        builder.HasData(
            new InboxMessageOffset
            {
                Id = 1,
                Topic = TopicNames.Topic1,
                Partition = 0
            },
            new InboxMessageOffset
            {
                Id = 2,
                Topic = TopicNames.Topic2,
                Partition = 0
            });
    }
}
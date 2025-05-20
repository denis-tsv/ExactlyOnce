using ExactlyOnce.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExactlyOnce.Db.EntityTypeConfigurations;

public class ProcessedDataEntityTypeConfiguration : IEntityTypeConfiguration<ProcessedData>
{
    public void Configure(EntityTypeBuilder<ProcessedData> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Data).HasColumnType("jsonb");
    }
}
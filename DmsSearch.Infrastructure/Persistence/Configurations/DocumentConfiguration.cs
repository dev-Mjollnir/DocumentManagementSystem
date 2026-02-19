using DmsSearch.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmsSearch.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).UseIdentityColumn();

        builder.Property(d => d.FileName).IsRequired().HasMaxLength(500);
        builder.Property(d => d.Category).HasMaxLength(100);
        builder.Property(d => d.Tags).HasMaxLength(1000);
        builder.Property(d => d.UploadedBy).IsRequired().HasMaxLength(200);
        builder.Property(d => d.UploadedAt).IsRequired();
        builder.Property(d => d.FileSizeBytes).IsRequired();
        builder.Property(d => d.FileHash).IsRequired().HasMaxLength(64);
        builder.Property(d => d.StoragePath).HasMaxLength(500);

        // Computed column — SQL Server maintains this automatically on insert/update
        builder.Property(d => d.SearchVector)
            .HasMaxLength(2000)
            .HasComputedColumnSql(
                "LOWER(ISNULL([FileName], '') + ' ' + ISNULL([Category], '') + ' ' + ISNULL([Tags], ''))",
                stored: true);

        builder.HasIndex(d => d.FileHash)
            .HasFilter("[FileHash] IS NOT NULL")
            .HasDatabaseName("IX_Documents_FileHash");

        builder.HasIndex(d => d.SearchVector)
            .HasDatabaseName("IX_Documents_SearchVector");
    }
}

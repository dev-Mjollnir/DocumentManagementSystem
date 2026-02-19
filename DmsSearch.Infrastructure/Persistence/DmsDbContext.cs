using DmsSearch.Domain.Entities;
using DmsSearch.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DmsSearch.Infrastructure.Persistence;

public sealed class DmsDbContext(DbContextOptions<DmsDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DocumentConfiguration());
    }
}

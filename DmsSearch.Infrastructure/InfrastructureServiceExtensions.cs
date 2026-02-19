using DmsSearch.Domain.Interfaces;
using DmsSearch.Infrastructure.Persistence;
using DmsSearch.Infrastructure.Search;
using DmsSearch.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DmsSearch.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DmsDbContext>(opts =>
            opts.UseSqlServer(configuration.GetConnectionString("Default"),
                sql => sql.MigrationsAssembly("DmsSearch.Infrastructure")));

        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentSearchService, LikeSearchService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
    }
}

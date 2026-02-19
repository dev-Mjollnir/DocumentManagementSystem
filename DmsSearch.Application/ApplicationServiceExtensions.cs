using DmsSearch.Application.Documents.Commands;
using DmsSearch.Application.Documents.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace DmsSearch.Application;

public static class ApplicationServiceExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SearchDocumentsHandler>();
        services.AddScoped<UploadDocumentHandler>();
    }
}

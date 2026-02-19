using DmsSearch.Application.Common;
using DmsSearch.Application.Documents.DTOs;
using DmsSearch.Domain.Entities;
using DmsSearch.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DmsSearch.Application.Documents.Queries;

public record SearchDocumentsQuery(
    string? Term,
    string? Category,
    DateTime? From,
    DateTime? To,
    int Page = 0,
    int PageSize = 20);

public sealed class SearchDocumentsHandler(
    IDocumentSearchService searchService,
    IDocumentRepository repository,
    IMemoryCache cache,
    ILogger<SearchDocumentsHandler> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<Result<SearchResultDto>> HandleAsync(
        SearchDocumentsQuery query, CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(query);

        if (cache.TryGetValue(cacheKey, out SearchResultDto? cached))
        {
            logger.LogDebug("Cache hit: {Key}", cacheKey);
            return Result<SearchResultDto>.Ok(cached! with { FromCache = true });
        }

        IReadOnlyList<Document> documents;
        int totalCount;

        if (string.IsNullOrWhiteSpace(query.Term))
        {
            var list = await repository.ListAsync(query.Page, query.PageSize, ct);
            documents = list;
            totalCount = list.Count;
        }
        else
        {
            var serviceQuery = new SearchQuery(
                query.Term, query.Category, query.From, query.To, query.Page, query.PageSize);

            var searchResult = await searchService.SearchAsync(serviceQuery, ct);
            documents = searchResult.Items;
            totalCount = searchResult.TotalCount;
        }

        var dtos = documents.Select(Map).ToList();

        var result = new SearchResultDto(
            Items: dtos,
            TotalCount: totalCount,
            FromCache: false,
            Suggestions: dtos.Count == 0 ? BuildSuggestions(query) : null);

        cache.Set(cacheKey, result, CacheTtl);

        logger.LogInformation("Search: Term={Term} Results={Count}", query.Term, dtos.Count);
        return Result<SearchResultDto>.Ok(result);
    }

    private static DocumentDto Map(Document d) => new(
        d.Id, d.FileName, d.Category, d.Tags,
        d.UploadedBy, d.UploadedAt, d.FileSizeBytes, d.FileHash);

    private static IReadOnlyList<string> BuildSuggestions(SearchDocumentsQuery q)
    {
        var hints = new List<string>
        {
            "Arama terimini kısaltmayı deneyin",
            "Tarih aralığını genişletin"
        };
        if (q.Category is not null)
            hints.Add("Kategori filtresini kaldırarak tekrar arayın");

        return hints;
    }

    private static string BuildCacheKey(SearchDocumentsQuery q) =>
        $"search:{q.Term?.ToLowerInvariant()}:{q.Category}:{q.From:yyyyMMdd}:{q.To:yyyyMMdd}:{q.Page}:{q.PageSize}";
}

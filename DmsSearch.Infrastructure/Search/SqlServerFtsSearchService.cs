using System.Text;
using DmsSearch.Domain.Entities;
using DmsSearch.Domain.Extensions;
using DmsSearch.Domain.Interfaces;
using DmsSearch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DmsSearch.Infrastructure.Search;

public sealed class LikeSearchService(DmsDbContext context, ILogger<LikeSearchService> logger) : IDocumentSearchService
{
    public async Task<SearchServiceResult> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        var terms = ParseTerms(query.Term);

        logger.LogDebug("LIKE search: terms={Terms}", string.Join(", ", terms));

        var q = context.Documents.AsNoTracking();

        // Each term must appear somewhere in SearchVector (AND logic)
        q = terms.Select(term => $"%{term}%").Aggregate(q,
            (current, pattern) => current.Where(d =>
                EF.Functions.Like(d.FileName.ToLower(), pattern) ||
                (d.Category != null && EF.Functions.Like(d.Category.ToLower(), pattern)) ||
                (d.Tags != null && EF.Functions.Like(d.Tags.ToLower(), pattern))));
        if (query.Category is not null)
            q = q.Where(d => d.Category == query.Category);

        if (query.From is not null)
            q = q.Where(d => d.UploadedAt >= query.From);

        if (query.To is not null)
            q = q.Where(d => d.UploadedAt <= query.To);

        var results = await q
            .OrderByDescending(d => d.UploadedAt)
            .Skip(query.Page * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new SearchServiceResult(results, results.Count);
    }

    private static IReadOnlyList<string> ParseTerms(string? term) =>
        (term ?? string.Empty)
        .ToLowerInvariant()
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(t => t.Length >= 2)
        .Take(10)
        .Select(StringExtensions.NormalizeTurkish)
        .ToList();
}
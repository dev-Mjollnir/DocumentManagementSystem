using DmsSearch.Domain.Entities;

namespace DmsSearch.Domain.Interfaces;

public interface IDocumentRepository
{
    Task<Document?> GetByHashAsync(string hash, CancellationToken ct = default);
    Task<int> AddAsync(Document document, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}

public interface IDocumentSearchService
{
    Task<SearchServiceResult> SearchAsync(SearchQuery query, CancellationToken ct = default);
}

public record SearchQuery(
    string? Term,
    string? Category,
    DateTime? From,
    DateTime? To,
    int Page = 0,
    int PageSize = 20);

public record SearchServiceResult(
    IReadOnlyList<Document> Items,
    int TotalCount);

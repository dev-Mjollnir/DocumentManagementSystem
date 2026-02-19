using DmsSearch.Domain.Entities;
using DmsSearch.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DmsSearch.Infrastructure.Persistence;

public sealed class DocumentRepository(DmsDbContext context) : IDocumentRepository
{
    public Task<Document?> GetByHashAsync(string hash, CancellationToken ct = default) =>
        context.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.FileHash == hash, ct);

    public async Task<int> AddAsync(Document document, CancellationToken ct = default)
    {
        context.Documents.Add(document);
        await context.SaveChangesAsync(ct);
        return document.Id;
    }

    public async Task<IReadOnlyList<Document>> ListAsync(int page, int pageSize, CancellationToken ct = default) =>
        await context.Documents.AsNoTracking()
            .OrderByDescending(d => d.UploadedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
}

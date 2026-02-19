namespace DmsSearch.Domain.Entities;

public sealed class Document
{
    public int Id { get; private set; }
    public string FileName { get; private set; } = null!;
    public string? Category { get; private set; }
    public string? Tags { get; private set; }
    public string UploadedBy { get; private set; } = null!;
    public DateTime UploadedAt { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string FileHash { get; private set; } = null!;
    public string? StoragePath { get; private set; }

    // Computed by SQL Server: LOWER(FileName + Category + Tags)
    // Used for LIKE-based search — read-only from application side
    public string? SearchVector { get; private set; }

    private Document() { }

    public static Document Create(
        string fileName, string? category, string? tags,
        string uploadedBy, long fileSizeBytes,
        string fileHash, string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileHash);

        return new Document
        {
            FileName = fileName.Trim(),
            Category = category?.Trim(),
            Tags = tags?.Trim(),
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = fileSizeBytes,
            FileHash = fileHash,
            StoragePath = storagePath
        };
    }
}

namespace DmsSearch.Domain.Interfaces;

public interface IFileStorageService
{
    Task<FileStorageResult> SaveAsync(Stream stream, string originalFileName, CancellationToken ct = default);
}

public record FileStorageResult(string StoragePath, string FileHash, long FileSizeBytes);

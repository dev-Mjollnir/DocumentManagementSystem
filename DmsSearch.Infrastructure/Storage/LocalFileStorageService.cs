using System.Security.Cryptography;
using System.Text;
using DmsSearch.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DmsSearch.Infrastructure.Storage;

/// <summary>
/// Writes file to local disk and computes SHA-256 hash in a SINGLE PASS.
/// CryptoStream wraps the destination so we hash while writing — no second read.
/// Also applies Unicode NFD normalization to file names before storage
/// to improve Turkish FTS matching (ş→s, ç→c folding at query time).
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration config, ILogger<LocalFileStorageService> logger)
    {
        _basePath = config["Storage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "dms-uploads");
        _logger = logger;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<FileStorageResult> SaveAsync(
        Stream stream, string originalFileName, CancellationToken ct = default)
    {
        var safeFileName = $"{Guid.NewGuid()}_{SanitizeFileName(originalFileName)}";
        var fullPath = Path.Combine(_basePath, safeFileName);

        string hash;
        long bytesWritten;

        // Single-pass: SHA256 hash computed while writing to disk
        using (var sha = SHA256.Create())
        await using (var destination = File.Create(fullPath))
        await using (var cryptoStream = new CryptoStream(destination, sha, CryptoStreamMode.Write))
        {
            await stream.CopyToAsync(cryptoStream, ct);
            await cryptoStream.FlushFinalBlockAsync(ct);

            bytesWritten = destination.Length;
            hash = Convert.ToHexString(sha.Hash!);
        }

        _logger.LogDebug("Stored {Path}, size={Bytes}, hash={Hash}", fullPath, bytesWritten, hash);

        return new FileStorageResult(fullPath, hash, bytesWritten);
    }

    // Unicode NFD normalization helps Turkish FTS matching.
    // e.g. "sözleşme" → normalized form that FTS tokenizer handles consistently.
    private static string SanitizeFileName(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var safe = string.Concat(normalized
            .Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        return safe.Length > 200 ? safe[..200] : safe;
    }
}

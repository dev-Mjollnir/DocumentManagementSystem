using System.Text;
using DmsSearch.Application.Common;
using DmsSearch.Application.Documents.DTOs;
using DmsSearch.Domain.Entities;
using DmsSearch.Domain.Extensions;
using DmsSearch.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DmsSearch.Application.Documents.Commands;

public record UploadDocumentCommand(
    Stream FileStream,
    string OriginalFileName,
    string? Category,
    string? Tags,
    string UploadedBy);

/// <summary>
/// Distinct error case so the controller can return a structured 409 — not an exception.
/// </summary>
public record DuplicateDocumentError(DuplicateInfoDto Existing);

public sealed class UploadDocumentHandler(
    IDocumentRepository repository,
    IFileStorageService storage,
    ILogger<UploadDocumentHandler> logger)
{
    /// <summary>
    /// Returns Ok(UploadResultDto) on success, Fail with duplicate message on conflict.
    /// Caller checks Result.IsSuccess — no exception-based flow control.
    /// </summary>
    public async Task<(Result<UploadResultDto> Result, DuplicateDocumentError? Duplicate)> HandleAsync(
        UploadDocumentCommand command, CancellationToken ct = default)
    {
        // Save file to storage and compute hash in single pass (no double-read)
        var stored = await storage.SaveAsync(command.FileStream, command.OriginalFileName, ct);

        // Check duplicate by hash
        var existing = await repository.GetByHashAsync(stored.FileHash, ct);
        if (existing is not null)
        {
            logger.LogWarning("Duplicate blocked: {Hash} → existing Id={Id}", stored.FileHash, existing.Id);

            var duplicate = new DuplicateDocumentError(new DuplicateInfoDto(
                existing.Id, existing.FileName, existing.UploadedAt, existing.UploadedBy));

            return (Result<UploadResultDto>.Fail("Duplicate"), duplicate);
        }

        var normalizedFileName = StringExtensions.NormalizeTurkish(command.OriginalFileName);

        var document = Document.Create(
            normalizedFileName,
            command.Category,
            command.Tags,
            command.UploadedBy,
            stored.FileSizeBytes,
            stored.FileHash,
            stored.StoragePath);

        var newId = await repository.AddAsync(document, ct);

        logger.LogInformation("Document saved. Id={Id} Hash={Hash}", newId, stored.FileHash);

        return (Result<UploadResultDto>.Ok(
            new UploadResultDto(newId, normalizedFileName, stored.FileHash, DateTime.UtcNow)), null);
    }
}

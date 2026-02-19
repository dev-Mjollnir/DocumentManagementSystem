namespace DmsSearch.Application.Documents.DTOs;

public record DocumentDto(
    int Id,
    string FileName,
    string? Category,
    string? Tags,
    string UploadedBy,
    DateTime UploadedAt,
    long FileSizeBytes,
    string FileHash
);

public record SearchResultDto(
    IReadOnlyList<DocumentDto> Items,
    int TotalCount,
    bool FromCache,
    IReadOnlyList<string>? Suggestions = null
);

public record DuplicateInfoDto(
    int ExistingId,
    string FileName,
    DateTime UploadedAt,
    string UploadedBy
);

public record UploadResultDto(
    int Id,
    string FileName,
    string FileHash,
    DateTime UploadedAt
);

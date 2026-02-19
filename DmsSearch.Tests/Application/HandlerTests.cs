using DmsSearch.Application.Documents.Commands;
using DmsSearch.Application.Documents.Queries;
using DmsSearch.Domain.Entities;
using DmsSearch.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DmsSearch.Tests.Application;

public class UploadDocumentHandlerTests
{
    private readonly Mock<IDocumentRepository> _repoMock = new();
    private readonly Mock<IFileStorageService> _storageMock = new();

    private UploadDocumentHandler CreateHandler() => new(
        _repoMock.Object, _storageMock.Object,
        NullLogger<UploadDocumentHandler>.Instance);

    [Fact]
    public async Task HandleAsync_NewFile_SavesAndReturnsOk()
    {
        // Arrange
        _storageMock.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), default))
            .ReturnsAsync(new FileStorageResult("/tmp/file.pdf", "NEWHASH", 1024));

        _repoMock.Setup(r => r.GetByHashAsync("NEWHASH", default))
            .ReturnsAsync((Document?)null);   // not a duplicate

        _repoMock.Setup(r => r.AddAsync(It.IsAny<Document>(), default))
            .ReturnsAsync(42);

        var handler = CreateHandler();
        var command = new UploadDocumentCommand(
            Stream.Null, "contract.pdf", "Sözleşme", null, "alice");

        // Act
        var (result, duplicate) = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        duplicate.Should().BeNull();
        result.Value!.Id.Should().Be(42);
        result.Value.FileName.Should().Be("contract.pdf");
    }

    [Fact]
    public async Task HandleAsync_DuplicateFile_ReturnsDuplicateError()
    {
        // Arrange
        _storageMock.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), default))
            .ReturnsAsync(new FileStorageResult("/tmp/file.pdf", "EXISTINGHASH", 1024));

        var existingDoc = Document.Create("old-contract.pdf", null, null, "bob", 1024, "EXISTINGHASH", "/tmp/old.pdf");

        _repoMock.Setup(r => r.GetByHashAsync("EXISTINGHASH", default))
            .ReturnsAsync(existingDoc);

        var handler = CreateHandler();
        var command = new UploadDocumentCommand(Stream.Null, "contract.pdf", null, null, "alice");

        // Act
        var (result, duplicate) = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        duplicate.Should().NotBeNull();
        duplicate!.Existing.FileName.Should().Be("old-contract.pdf");

        // Ensure document was NOT re-saved
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Document>(), default), Times.Never);
    }
}

public class SearchDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_EmptyTerm_UsesRepositoryNotSearchService()
    {
        // Arrange
        var repoMock = new Mock<IDocumentRepository>();
        var searchMock = new Mock<IDocumentSearchService>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        repoMock.Setup(r => r.ListAsync(0, 20, default))
            .ReturnsAsync(new List<Document>());

        var handler = new SearchDocumentsHandler(
            searchMock.Object, repoMock.Object, cache,
            NullLogger<SearchDocumentsHandler>.Instance);

        // Act
        await handler.HandleAsync(new SearchDocumentsQuery(null, null, null, null));

        // Assert: FTS service was NOT called for empty query
        searchMock.Verify(s => s.SearchAsync(It.IsAny<SearchQuery>(), default), Times.Never);
        repoMock.Verify(r => r.ListAsync(0, 20, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithTerm_UsesFtsSearchService()
    {
        var repoMock = new Mock<IDocumentRepository>();
        var searchMock = new Mock<IDocumentSearchService>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        searchMock.Setup(s => s.SearchAsync(It.IsAny<SearchQuery>(), default))
            .ReturnsAsync(new SearchServiceResult(new List<Document>(), 0));

        var handler = new SearchDocumentsHandler(
            searchMock.Object, repoMock.Object, cache,
            NullLogger<SearchDocumentsHandler>.Instance);

        // Act
        var result = await handler.HandleAsync(new SearchDocumentsQuery("sözleşme", null, null, null));

        // Assert
        result.IsSuccess.Should().BeTrue();
        searchMock.Verify(s => s.SearchAsync(It.IsAny<SearchQuery>(), default), Times.Once);
        repoMock.Verify(r => r.ListAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SecondCallSameQuery_ReturnsCachedResult()
    {
        var repoMock = new Mock<IDocumentRepository>();
        var searchMock = new Mock<IDocumentSearchService>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        searchMock.Setup(s => s.SearchAsync(It.IsAny<SearchQuery>(), default))
            .ReturnsAsync(new SearchServiceResult(new List<Document>(), 0));

        var handler = new SearchDocumentsHandler(
            searchMock.Object, repoMock.Object, cache,
            NullLogger<SearchDocumentsHandler>.Instance);

        var query = new SearchDocumentsQuery("fatura", null, null, null);

        // Act - call twice
        await handler.HandleAsync(query);
        var second = await handler.HandleAsync(query);

        // Assert: FTS hit only once, second call served from cache
        searchMock.Verify(s => s.SearchAsync(It.IsAny<SearchQuery>(), default), Times.Once);
        second.Value!.FromCache.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_NoResults_IncludesSuggestions()
    {
        var repoMock = new Mock<IDocumentRepository>();
        var searchMock = new Mock<IDocumentSearchService>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        searchMock.Setup(s => s.SearchAsync(It.IsAny<SearchQuery>(), default))
            .ReturnsAsync(new SearchServiceResult(new List<Document>(), 0));

        var handler = new SearchDocumentsHandler(
            searchMock.Object, repoMock.Object, cache,
            NullLogger<SearchDocumentsHandler>.Instance);

        // Act
        var result = await handler.HandleAsync(new SearchDocumentsQuery("xyz", "Fatura", null, null));

        // Assert
        result.Value!.Items.Should().BeEmpty();
        result.Value.Suggestions.Should().NotBeNullOrEmpty();
    }
}

public class LocalFileStorageTests
{
    [Fact]
    public async Task SaveAsync_SameContent_ProducesSameHash()
    {
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["Storage:BasePath"]).Returns(Path.GetTempPath());

        var svc = new DmsSearch.Infrastructure.Storage.LocalFileStorageService(
            configMock.Object,
            NullLogger<DmsSearch.Infrastructure.Storage.LocalFileStorageService>.Instance);

        var content = "test document content"u8.ToArray();

        await using var s1 = new MemoryStream(content);
        await using var s2 = new MemoryStream(content);

        var r1 = await svc.SaveAsync(s1, "test1.pdf");
        var r2 = await svc.SaveAsync(s2, "test2.pdf");

        r1.FileHash.Should().Be(r2.FileHash);
        r1.FileSizeBytes.Should().Be(content.Length);
    }
}

using DmsSearch.Application.Documents.Queries;
using Microsoft.AspNetCore.Mvc;

namespace DmsSearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class DocumentsController(SearchDocumentsHandler handler) : ControllerBase
{
    /// <summary>Search or list documents. Returns suggestions when no results found.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var query = new SearchDocumentsQuery(q, category, from, to, page, pageSize);
        var result = await handler.HandleAsync(query, ct);

        // Result<T> is always Ok here — search never fails, it returns empty
        return Ok(result.Value);
    }
}

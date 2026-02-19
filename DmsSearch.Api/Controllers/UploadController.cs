using DmsSearch.Application.Documents.Commands;
using Microsoft.AspNetCore.Mvc;

namespace DmsSearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class UploadController(UploadDocumentHandler handler) : ControllerBase
{
    /// <summary>
    /// Upload a document. Returns 409 with existing document info if a duplicate is detected.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string? category,
        [FromForm] string? tags,
        CancellationToken ct = default)
    {
        if (file.Length == 0)
            return BadRequest(new { message = "Dosya boş olamaz." });

        var command = new UploadDocumentCommand(
            FileStream: file.OpenReadStream(),
            OriginalFileName: file.FileName,
            Category: category,
            Tags: tags,
            UploadedBy: HttpContext.User.Identity?.Name ?? "anonymous");

        var (result, duplicate) = await handler.HandleAsync(command, ct);

        if (duplicate is not null)
        {
            return Conflict(new
            {
                message = $"Bu dosya daha önce '{duplicate.Existing.FileName}' adıyla yüklenmiş.",
                existing = duplicate.Existing,
                hint = "Yeni versiyon olarak yüklemek için lütfen destek ekibiyle iletişime geçin."
            });
        }

        return CreatedAtAction(
            nameof(DocumentsController.Search),
            "Documents",
            new { q = result.Value!.FileName },
            result.Value);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Core.Interfaces;
using Server.Core.Models;

namespace SyncServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpGet("state")]
    public async Task<ActionResult<SyncState>> GetSyncState(CancellationToken ct)
    {
        var state = await _fileService.GetSyncStateAsync(ct);
        return Ok(state);
    }

    [HttpPut("{*path}")]
    public async Task<IActionResult> UploadFile(string path, [FromQuery] string hash, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не выбран или пуст");
        }

        using var stream = file.OpenReadStream();
        await _fileService.UploadFileAsync(path, stream, hash, ct);

        return Ok(new { message = "Файл загружен", path, size = file.Length });
    }

    [HttpGet("{*path}")]
    public async Task<IActionResult> DownloadFile(string path, CancellationToken ct)
    {
        var stream = await _fileService.DownloadFileAsync(path, ct);
        return File(stream, "application/octet-stream", Path.GetFileName(path));
    }

    [HttpDelete("{*path}")]
    public async Task<IActionResult> DeleteFile(string path, CancellationToken ct)
    {
        await _fileService.DeleteFileAsync(path, ct);
        return Ok();
    }
}

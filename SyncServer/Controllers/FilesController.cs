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

    /// <summary>
    /// Декодирует путь, сохраняя / как разделитель папок
    /// </summary>
    private static string DecodePath(string path)
    {
        return string.Join("/", path.Split('/').Select(Uri.UnescapeDataString));
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
        if (file == null)
        {
            return BadRequest(new { error = "Файл не выбран" });
        }

        // ✅ ИСПРАВЛЕНО: декодируем путь
        var decodedPath = DecodePath(path);

        using var stream = file.OpenReadStream();
        await _fileService.UploadFileAsync(decodedPath, stream, hash, ct);

        return Ok(new { message = "Файл загружен", path = decodedPath, size = file.Length });
    }

    [HttpGet("{*path}")]
    public async Task<IActionResult> DownloadFile(string path, CancellationToken ct)
    {
        // ✅ ИСПРАВЛЕНО: декодируем путь
        var decodedPath = DecodePath(path);

        var stream = await _fileService.DownloadFileAsync(decodedPath, ct);
        return File(stream, "application/octet-stream", Path.GetFileName(decodedPath));
    }

    [HttpDelete("{*path}")]
    public async Task<IActionResult> DeleteFile(string path, CancellationToken ct)
    {
        // ✅ ИСПРАВЛЕНО: декодируем путь
        var decodedPath = DecodePath(path);

        await _fileService.DeleteFileAsync(decodedPath, ct);
        return Ok();
    }
}
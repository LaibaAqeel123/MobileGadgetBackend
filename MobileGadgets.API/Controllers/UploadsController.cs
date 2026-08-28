using Microsoft.AspNetCore.Mvc;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.API.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IImageStorageService _imageStorage;

    public UploadsController(IImageStorageService imageStorage)
    {
        _imageStorage = imageStorage;
    }

    [HttpPost("image")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file.Length == 0) return BadRequest(new { error = "No file provided." });

        await using var stream = file.OpenReadStream();
        var url = await _imageStorage.SaveImageAsync(stream, file.FileName);

        return Ok(new { url });
    }
}

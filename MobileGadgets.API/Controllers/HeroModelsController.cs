using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.API.Controllers;

[ApiController]
[Route("heromodels")]
[Authorize]
public class HeroModelsController : ControllerBase
{
    private readonly IHeroModelService _heroModelService;

    public HeroModelsController(IHeroModelService heroModelService)
    {
        _heroModelService = heroModelService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _heroModelService.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var model = await _heroModelService.GetByIdAsync(id);
        return model is null ? NotFound() : Ok(model);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateHeroModelRequest request)
    {
        var created = await _heroModelService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHeroModelRequest request)
    {
        var updated = await _heroModelService.UpdateAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _heroModelService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Admin-only preview step for a Tilted HeroModel: runs auto-detection on an uploaded
    /// photo and returns the resulting masks + geometry. Saves nothing — the admin reviews this
    /// preview (with a test design, client-side) and only persists it by calling Create/Update
    /// afterward with PoseType=Tilted and these same values. cameraRoundR/cameraPadPx let the
    /// admin re-run this with a nudged camera-area roundness/margin if the first preview isn't
    /// quite right for this particular photo.</summary>
    [HttpPost("detect-tilted")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> DetectTilted(
        IFormFile photo, [FromForm] double? cameraRoundR = null, [FromForm] double? cameraPadPx = null)
    {
        if (photo.Length == 0) return BadRequest(new { error = "No photo file provided." });

        try
        {
            await using var photoStream = photo.OpenReadStream();
            var result = await _heroModelService.DetectTiltedAsync(photoStream, cameraRoundR, cameraPadPx);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Renders one test composite from an in-progress (not-yet-saved) Tilted preview —
    /// pass back the exact TiltedDetectionPreviewDto detect-tilted just returned, plus any sample
    /// design image, and get back an actual rendered PNG to show the admin before they decide to
    /// save. Saves nothing except the rendered preview image itself.</summary>
    [HttpPost("preview-tilted")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> PreviewTilted(
        [FromForm] string baseImageUrl,
        [FromForm] string silhouetteImageUrl,
        [FromForm] string cameraMaskImageUrl,
        [FromForm] string overlayImageUrl,
        [FromForm] string designQuadJson,
        [FromForm] string caseBboxJson,
        IFormFile design)
    {
        if (design.Length == 0) return BadRequest(new { error = "No design file provided." });

        var preview = new TiltedDetectionPreviewDto
        {
            BaseImageUrl = baseImageUrl,
            SilhouetteImageUrl = silhouetteImageUrl,
            CameraMaskImageUrl = cameraMaskImageUrl,
            OverlayImageUrl = overlayImageUrl,
            DesignQuadJson = designQuadJson,
            CaseBboxJson = caseBboxJson,
        };

        await using var designStream = design.OpenReadStream();
        var png = await _heroModelService.PreviewTiltedRenderAsync(preview, designStream);
        return File(png, "image/png");
    }
}

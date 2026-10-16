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
        try
        {
            var created = await _heroModelService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHeroModelRequest request)
    {
        try
        {
            var updated = await _heroModelService.UpdateAsync(id, request);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _heroModelService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Admin-only, step 1 of saving a 3D model: stores the uploaded .glb and checks it.
    /// Returns the stored file's URL plus a report (errors block saving, warnings do not) and the
    /// measured print layout. Saves nothing to the database — the admin reviews the report and, if
    /// it is clean, calls Create with PoseType=Model3D and the returned glbUrl/printMaterialName.
    /// A bad file is not an HTTP error: it comes back as a report with errors, so the admin sees why.</summary>
    [HttpPost("inspect-glb")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> InspectGlb(IFormFile glb, [FromForm] string? printMaterialName)
    {
        if (glb.Length == 0) return BadRequest(new { error = "No file provided." });

        await using var stream = glb.OpenReadStream();
        return Ok(await _heroModelService.InspectGlbAsync(stream, printMaterialName));
    }

    /// <summary>Re-checks an already-uploaded .glb against a different printable material — what
    /// the admin does after picking one from the material list when the file has no PRINT_AREA.</summary>
    [HttpPost("reinspect-glb")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReinspectGlb([FromForm] string glbUrl, [FromForm] string? printMaterialName)
    {
        try
        {
            return Ok(await _heroModelService.ReinspectGlbAsync(glbUrl, printMaterialName));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Admin-only preview step for a Tilted HeroModel: runs auto-detection on an uploaded
    /// photo and returns the resulting masks + geometry, including a starting camera-area OUTLINE
    /// as real editable points (CameraOutlinePointsJson) — seeds the point editor with an
    /// already-close guess. Saves nothing — the admin reviews this preview (with a test design,
    /// client-side), refines the outline/spots with the point editor (see bake-camera-mask), and
    /// only persists it by calling Create/Update afterward with PoseType=Tilted.</summary>
    [HttpPost("detect-tilted")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> DetectTilted(IFormFile photo)
    {
        if (photo.Length == 0) return BadRequest(new { error = "No photo file provided." });

        try
        {
            await using var photoStream = photo.OpenReadStream();
            var result = await _heroModelService.DetectTiltedAsync(photoStream);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Fast path the point editor calls after every edit: rasterizes a fresh camera mask
    /// directly from the admin's current outline points + spot overrides (no re-detection at all —
    /// a couple seconds, not ~30s) and saves it. Returns the new mask's URL; follow with
    /// preview-tilted (passing this URL as cameraMaskImageUrl) to actually see the design
    /// applied.</summary>
    [HttpPost("bake-camera-mask")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BakeCameraMask(
        [FromForm] string baseImageUrl, [FromForm] string cameraOutlinePointsJson, [FromForm] string cameraSpotsJson)
    {
        var url = await _heroModelService.BakeCameraMaskAsync(baseImageUrl, cameraOutlinePointsJson, cameraSpotsJson);
        return Ok(new { cameraMaskImageUrl = url });
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

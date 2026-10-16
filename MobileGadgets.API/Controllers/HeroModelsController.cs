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

    /// <summary>Admin-only, step 1 of saving a 3D model: stores the uploaded .glb and checks it.
    /// Returns the stored file's URL plus a report (errors block saving, warnings do not) and the
    /// measured print layout. Saves nothing to the database — the admin reviews the report and, if
    /// it is clean, calls Create with the returned glbUrl/printMaterialName.
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

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _heroModelService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}

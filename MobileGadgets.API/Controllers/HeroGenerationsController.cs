using Microsoft.AspNetCore.Mvc;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.API.Controllers;

[ApiController]
[Route("api/herogenerations")]
public class HeroGenerationsController : ControllerBase
{
    private readonly IHeroGenerationService _generationService;

    public HeroGenerationsController(IHeroGenerationService generationService)
    {
        _generationService = generationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _generationService.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var generation = await _generationService.GetByIdAsync(id);
        return generation is null ? NotFound() : Ok(generation);
    }

    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Generate([FromForm] int heroModelId, [FromForm] int? sceneId, IFormFile design)
    {
        if (design.Length == 0) return BadRequest(new { error = "No design file provided." });

        try
        {
            await using var designStream = design.OpenReadStream();
            var result = await _generationService.GenerateAsync(heroModelId, designStream, design.FileName, sceneId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

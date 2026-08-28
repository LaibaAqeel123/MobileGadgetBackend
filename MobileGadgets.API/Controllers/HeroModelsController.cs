using Microsoft.AspNetCore.Mvc;
using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.API.Controllers;

[ApiController]
[Route("api/heromodels")]
public class HeroModelsController : ControllerBase
{
    private readonly IHeroModelService _heroModelService;
    private readonly IHeroImageRenderer _renderer;
    private readonly IImageStorageService _imageStorage;

    public HeroModelsController(IHeroModelService heroModelService, IHeroImageRenderer renderer, IImageStorageService imageStorage)
    {
        _heroModelService = heroModelService;
        _renderer = renderer;
        _imageStorage = imageStorage;
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
    public async Task<IActionResult> Create([FromBody] CreateHeroModelRequest request)
    {
        var created = await _heroModelService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHeroModelRequest request)
    {
        var updated = await _heroModelService.UpdateAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _heroModelService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Sprint 2 engine verification only — runs the renderer against a stored HeroModel
    /// and returns the PNG directly, no persistence. Sprint 3 replaces this with the real,
    /// permanent Generate flow (HeroGeneration record + stored output).</summary>
    [HttpPost("{id:int}/render-preview")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> RenderPreview(int id, IFormFile design)
    {
        var model = await _heroModelService.GetByIdAsync(id);
        if (model is null) return NotFound();

        using var baseStream = _imageStorage.OpenRead(model.BaseImageUrl);
        using var designMaskStream = _imageStorage.OpenRead(model.DesignMaskImageUrl);
        using var cameraMaskStream = _imageStorage.OpenRead(model.CameraMaskImageUrl);
        using var overlayStream = _imageStorage.OpenRead(model.OverlayImageUrl);
        using var designStream = design.OpenReadStream();

        var png = await _renderer.RenderAsync(baseStream, designMaskStream, cameraMaskStream, overlayStream, designStream);
        return File(png, "image/png");
    }
}

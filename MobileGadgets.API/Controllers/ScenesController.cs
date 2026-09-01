using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.API.Controllers;

[ApiController]
[Route("scenes")]
[Authorize]
public class ScenesController : ControllerBase
{
    private readonly ISceneService _sceneService;

    public ScenesController(ISceneService sceneService)
    {
        _sceneService = sceneService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _sceneService.GetAllAsync());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateSceneRequest request) =>
        Ok(await _sceneService.CreateAsync(request));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _sceneService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}

using Microsoft.AspNetCore.Mvc;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.API.Controllers;

[ApiController]
[Route("api/scenes")]
public class ScenesController : ControllerBase
{
    private readonly ISceneService _sceneService;

    public ScenesController(ISceneService sceneService)
    {
        _sceneService = sceneService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _sceneService.GetAllAsync());
}

using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.Application.Services;

public class SceneService : ISceneService
{
    private readonly ISceneRepository _repository;

    public SceneService(ISceneRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<SceneDto>> GetAllAsync()
    {
        var scenes = await _repository.GetAllAsync();
        return scenes.Select(s => new SceneDto
        {
            Id = s.Id,
            Name = s.Name,
            IsDefault = s.IsDefault,
            BackgroundTopColor = s.BackgroundTopColor,
            BackgroundBottomColor = s.BackgroundBottomColor,
        }).ToList();
    }
}

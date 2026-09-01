using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

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
        return scenes.Select(ToDto).ToList();
    }

    public async Task<SceneDto> CreateAsync(CreateSceneRequest request)
    {
        // Pose matches the one approved default (Dark Studio's "room_polish_v1" pose) — every
        // background preset shares the same camera/lean/yaw, since that's a geometry decision
        // made once, not something to re-tune per background.
        var scene = new Scene
        {
            Name = request.Name,
            IsDefault = false,
            CamY = 1.35,
            CamZ = -2.1,
            PitchDegrees = 9,
            Focal = 1500,
            LeanDegrees = 5,
            YawDegrees = 0,
            BackgroundImageUrl = request.BackgroundImageUrl,
        };

        await _repository.AddAsync(scene);
        await _repository.SaveChangesAsync();

        return ToDto(scene);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var scene = await _repository.GetByIdAsync(id);
        if (scene is null || scene.IsDefault) return false;

        _repository.Remove(scene);
        await _repository.SaveChangesAsync();
        return true;
    }

    private static SceneDto ToDto(Scene s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        IsDefault = s.IsDefault,
        BackgroundTopColor = s.BackgroundTopColor,
        BackgroundBottomColor = s.BackgroundBottomColor,
        BackgroundImageUrl = s.BackgroundImageUrl,
    };
}

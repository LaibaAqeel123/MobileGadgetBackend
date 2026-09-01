using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface ISceneService
{
    Task<List<SceneDto>> GetAllAsync();
    Task<SceneDto> CreateAsync(CreateSceneRequest request);

    /// <summary>Returns false if the Scene doesn't exist or is the default Scene (kept as a
    /// permanent fallback, never deletable).</summary>
    Task<bool> DeleteAsync(int id);
}

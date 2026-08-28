using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface ISceneService
{
    Task<List<SceneDto>> GetAllAsync();
}

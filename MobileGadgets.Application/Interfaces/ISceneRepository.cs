using MobileGadgets.Domain;

namespace MobileGadgets.Application.Interfaces;

public interface ISceneRepository
{
    Task<List<Scene>> GetAllAsync();
    Task<Scene?> GetByIdAsync(int id);
    Task<Scene> GetDefaultAsync();
}

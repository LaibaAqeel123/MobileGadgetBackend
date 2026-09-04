using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Application.Services;

public class HeroModelService : IHeroModelService
{
    private readonly IHeroModelRepository _repository;

    public HeroModelService(IHeroModelRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<HeroModelDto>> GetAllAsync()
    {
        var models = await _repository.GetAllAsync();
        return models.Select(ToDto).ToList();
    }

    public async Task<HeroModelDto?> GetByIdAsync(int id)
    {
        var model = await _repository.GetByIdAsync(id);
        return model is null ? null : ToDto(model);
    }

    public async Task<HeroModelDto> CreateAsync(CreateHeroModelRequest request)
    {
        var heroModel = new HeroModel
        {
            PhoneName = request.PhoneName,
            CaseType = request.CaseType,
            PoseType = request.PoseType,
            BaseImageUrl = request.BaseImageUrl,
            DesignMaskImageUrl = request.DesignMaskImageUrl,
            CameraMaskImageUrl = request.CameraMaskImageUrl,
            OverlayImageUrl = request.OverlayImageUrl,
            DesignQuadJson = request.DesignQuadJson,
            CreatedAt = DateTime.UtcNow,
        };

        await _repository.AddAsync(heroModel);
        await _repository.SaveChangesAsync();

        return ToDto(heroModel);
    }

    public async Task<HeroModelDto?> UpdateAsync(int id, UpdateHeroModelRequest request)
    {
        var model = await _repository.GetByIdAsync(id);
        if (model is null) return null;

        model.PhoneName = request.PhoneName;
        model.CaseType = request.CaseType;
        model.PoseType = request.PoseType;
        model.BaseImageUrl = request.BaseImageUrl;
        model.DesignMaskImageUrl = request.DesignMaskImageUrl;
        model.CameraMaskImageUrl = request.CameraMaskImageUrl;
        model.OverlayImageUrl = request.OverlayImageUrl;
        model.DesignQuadJson = request.DesignQuadJson;

        await _repository.SaveChangesAsync();
        return ToDto(model);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var model = await _repository.GetByIdAsync(id);
        if (model is null) return false;

        _repository.Remove(model);
        await _repository.SaveChangesAsync();
        return true;
    }

    private static HeroModelDto ToDto(HeroModel model) => new()
    {
        Id = model.Id,
        PhoneName = model.PhoneName,
        CaseType = model.CaseType,
        PoseType = model.PoseType,
        BaseImageUrl = model.BaseImageUrl,
        DesignMaskImageUrl = model.DesignMaskImageUrl,
        CameraMaskImageUrl = model.CameraMaskImageUrl,
        OverlayImageUrl = model.OverlayImageUrl,
        DesignQuadJson = model.DesignQuadJson,
        CreatedAt = model.CreatedAt,
    };
}

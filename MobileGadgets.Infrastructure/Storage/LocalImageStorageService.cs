using Microsoft.Extensions.Options;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.Infrastructure.Storage;

public class LocalImageStorageService : IImageStorageService
{
    private readonly StorageSettings _settings;

    public LocalImageStorageService(IOptions<StorageSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<string> SaveImageAsync(Stream content, string fileName)
    {
        Directory.CreateDirectory(_settings.LocalBasePath);

        var safeFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(_settings.LocalBasePath, safeFileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream);

        return $"{_settings.BaseUrl}/{safeFileName}";
    }

    public void DeleteFile(string url)
    {
        var fileName = url[(url.LastIndexOf('/') + 1)..];
        var fullPath = Path.Combine(_settings.LocalBasePath, fileName);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    public Stream OpenRead(string url)
    {
        var fileName = url[(url.LastIndexOf('/') + 1)..];
        var fullPath = Path.Combine(_settings.LocalBasePath, fileName);
        return File.OpenRead(fullPath);
    }
}

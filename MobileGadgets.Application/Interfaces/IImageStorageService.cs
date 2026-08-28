namespace MobileGadgets.Application.Interfaces;

public interface IImageStorageService
{
    /// <summary>Saves an image stream and returns its public URL path.</summary>
    Task<string> SaveImageAsync(Stream content, string fileName);

    void DeleteFile(string url);
}

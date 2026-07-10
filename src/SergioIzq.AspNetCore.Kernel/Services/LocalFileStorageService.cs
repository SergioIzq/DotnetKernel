using SergioIzq.Application.Kernel.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace SergioIzq.AspNetCore.Kernel.Services;

/// <summary>
/// Implementación de <see cref="IFileStorageService"/> sobre disco local (wwwroot),
/// generando la URL pública a partir del host de la request actual. En Docker, wwwroot
/// se mapea a un volumen del host.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalFileStorageService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
    {
        _env = env;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, string containerName)
    {
        string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        string folderPath = Path.Combine(webRootPath, containerName);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Nombre único para evitar colisiones y caracteres problemáticos
        string extension = Path.GetExtension(fileName);
        string newFileName = $"{Guid.NewGuid()}{extension}";
        string fullPath = Path.Combine(folderPath, newFileName);

        if (fileStream.CanSeek) fileStream.Position = 0;

        using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fs);
        }

        // URL pública a partir del dominio de la request actual
        var currentUrl = $"{_httpContextAccessor.HttpContext?.Request.Scheme}://{_httpContextAccessor.HttpContext?.Request.Host}";

        var pathForDb = Path.Combine(currentUrl, containerName, newFileName).Replace("\\", "/");

        return pathForDb;
    }

    public Task DeleteFileAsync(string? fileRoute, string containerName)
    {
        if (string.IsNullOrEmpty(fileRoute))
        {
            return Task.CompletedTask;
        }

        var fileName = Path.GetFileName(fileRoute);

        string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string filePath = Path.Combine(webRootPath, containerName, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}

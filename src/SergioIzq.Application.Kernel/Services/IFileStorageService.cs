namespace SergioIzq.Application.Kernel.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, string containerName);
    Task DeleteFileAsync(string? fileRoute, string containerName);
}

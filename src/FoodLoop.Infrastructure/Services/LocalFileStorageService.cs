using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;

namespace FoodLoop.Infrastructure.Services;

/// <summary>
/// Saves uploaded files to wwwroot/uploads/{folder}/ and returns a relative URL served as
/// static files (see Program.cs UseStaticFiles). This is a placeholder for the real Object
/// Storage integration (S3/Azure Blob/etc.) called out in the System Architecture doc —
/// swap the DI registration for a cloud-backed IFileStorageService when that's wired up.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;

    public LocalFileStorageService(string webRootPath)
    {
        _webRootPath = webRootPath;
    }

    public async Task<string> SaveAsync(FileUploadRequest file, string folder, CancellationToken cancellationToken = default)
    {
        var uploadsRoot = Path.Combine(_webRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadsRoot);

        var safeExtension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{safeExtension}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var outputStream = File.Create(fullPath))
        {
            await file.Content.CopyToAsync(outputStream, cancellationToken);
        }

        return $"/uploads/{folder}/{fileName}";
    }
}

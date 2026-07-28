using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;

namespace FoodLoop.Infrastructure.Services;

/// <summary>
/// Saves uploaded files to {webRoot}/uploads/{folder}/ and returns a relative URL served as
/// static files (see Program.cs UseStaticFiles). This is a placeholder for the real Object
/// Storage integration (S3/Azure Blob/etc.) called out in the System Architecture doc —
/// swap the DI registration for a cloud-backed IFileStorageService when that's wired up.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;

    public LocalFileStorageService(string webRootPath)
    {
        // Fall back to a sibling "wwwroot" of the working directory when the host
        // provides an empty web-root path (e.g. runasp.net shared hosting).
        _webRootPath = !string.IsNullOrWhiteSpace(webRootPath)
            ? webRootPath
            : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<string> SaveAsync(FileUploadRequest file, string folder, CancellationToken cancellationToken = default)
    {
        try
        {
            var uploadsRoot = Path.Combine(_webRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadsRoot);

            // Sanitize: keep only the extension, discard any path components in the original name.
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10)
                ext = ".bin";

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using var outputStream = File.Create(fullPath);
            await file.Content.CopyToAsync(outputStream, cancellationToken);

            return $"/uploads/{folder}/{fileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"File storage failed. Ensure the server has write access to '{_webRootPath}/uploads'. Inner: {ex.Message}", ex);
        }
    }
}

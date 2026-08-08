using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class CloudinaryFileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryFileStorageService> _logger;

    private static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp" };

    private static readonly string[] VideoExtensions =
        { ".mp4", ".mov", ".avi", ".mkv", ".webm" };

    public CloudinaryFileStorageService(Cloudinary cloudinary, ILogger<CloudinaryFileStorageService> logger)
    {
        _cloudinary = cloudinary;
        _logger = logger;
    }

    public async Task<string> SaveAsync(FileUploadRequest file, string folder, CancellationToken cancellationToken = default)
    {
        if (file?.Content == null)
            throw new ArgumentNullException(nameof(file));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(file.FileName);
        var uniqueName = $"{Guid.NewGuid()}_{baseName}";

        try
        {
            UploadResult uploadResult;

            if (Array.Exists(ImageExtensions, e => e == ext))
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(uniqueName + ext, file.Content),
                    Folder = folder,
                    UseFilename = false,
                    UniqueFilename = false,
                    Overwrite = true
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
            }
            else if (Array.Exists(VideoExtensions, e => e == ext))
            {
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(uniqueName + ext, file.Content),
                    Folder = folder,
                    UseFilename = false,
                    UniqueFilename = false,
                    Overwrite = true
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
            }
            else
            {
                // PDFs and all other document types — resource_type = raw
                // Must NOT pass CancellationToken to the raw overload in SDK v1.x
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(uniqueName + ext, file.Content),
                    Folder = folder,
                    UseFilename = false,
                    UniqueFilename = false,
                    Overwrite = true
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary upload error for {File}: {Error}", file.FileName, uploadResult.Error.Message);
                throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            _logger.LogInformation("Uploaded {File} to Cloudinary: {Url}", file.FileName, uploadResult.SecureUrl);
            return uploadResult.SecureUrl.ToString();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload {File} to Cloudinary", file.FileName);
            throw new InvalidOperationException($"Failed to upload file '{file.FileName}' to Cloudinary. Inner: {ex.Message}", ex);
        }
    }
}

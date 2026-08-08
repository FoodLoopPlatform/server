using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class CloudinaryFileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryFileStorageService(Cloudinary cloudinary)
    {
        _cloudinary = cloudinary;
    }

    public async Task<string> SaveAsync(FileUploadRequest file, string folder, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
            var publicId = $"{folder}/{Guid.NewGuid()}_{fileNameWithoutExt}";
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            // PDFs and other non-image files must use RawUploadParams
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };
            bool isImage = Array.Exists(imageExtensions, e => e == ext);

            string secureUrl;

            if (isImage)
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, file.Content),
                    PublicId = publicId,
                    Overwrite = true
                };
                var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
                if (result.Error != null)
                    throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
                secureUrl = result.SecureUrl.ToString();
            }
            else
            {
                // PDFs and other raw files — set ResourceType=Raw on ImageUploadParams
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, file.Content),
                    PublicId = publicId,
                    Overwrite = true,
                    Type = "upload"
                };
                // resource_type must be "raw" for non-image files
                uploadParams.AddCustomParam("resource_type", "raw");
                var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
                if (result.Error != null)
                    throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
                secureUrl = result.SecureUrl.ToString();
            }

            return secureUrl;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload file '{file.FileName}' to Cloudinary. Inner: {ex.Message}", ex);
        }
    }
}

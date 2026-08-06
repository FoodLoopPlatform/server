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
            // Prefix folder to segment files inside Cloudinary
            var publicId = $"{folder}/{Guid.NewGuid()}_{fileNameWithoutExt}";

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, file.Content),
                PublicId = publicId,
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload file '{file.FileName}' to Cloudinary. Inner: {ex.Message}", ex);
        }
    }
}

using FoodLoop.Application.Common.Models;

namespace FoodLoop.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the external Object Storage integration (see System Architecture,
/// section 5/6). Sprint 1 ships a local-disk implementation so the onboarding document
/// upload flow works end-to-end without cloud credentials; swap in an S3/Azure Blob-backed
/// implementation later without touching Application or API code.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Saves the file under the given logical folder and returns its public URL/path.</summary>
    Task<string> SaveAsync(FileUploadRequest file, string folder, CancellationToken cancellationToken = default);
}

using Microsoft.AspNetCore.Http;

namespace nettest.Services;

public record UploadedImage(string Url, string PublicId);

public interface IImageUploader
{
    Task<IReadOnlyList<UploadedImage>> UploadImagesAsync(
        IReadOnlyList<IFormFile> images,
        CancellationToken cancellationToken);
}

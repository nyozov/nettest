using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using nettest.Options;

namespace nettest.Services;

public class CloudinaryImageUploader(IOptions<CloudinaryOptions> options) : IImageUploader
{
    private const long MaxImageBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif"
        };

    private readonly CloudinaryOptions _options = options.Value;

    public async Task<IReadOnlyList<UploadedImage>> UploadImagesAsync(
        IReadOnlyList<IFormFile> images,
        CancellationToken cancellationToken)
    {
        if (images.Count == 0)
            return [];

        if (string.IsNullOrWhiteSpace(_options.CloudName) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException("Cloudinary is not configured.");
        }

        var uploadedImages = new List<UploadedImage>();
        foreach (var image in images)
        {
            ValidateImage(image);
            uploadedImages.Add(await UploadImageAsync(image, cancellationToken));
        }

        return uploadedImages;
    }

    private async Task<UploadedImage> UploadImageAsync(
        IFormFile image,
        CancellationToken cancellationToken)
    {
        var cloudName = _options.CloudName.Trim();
        var apiKey = _options.ApiKey.Trim();
        var apiSecret = _options.ApiSecret.Trim();
        var folder = string.IsNullOrWhiteSpace(_options.Folder)
            ? null
            : _options.Folder.Trim();

        var cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret))
        {
            Api = { Secure = true }
        };

        await using var imageStream = image.OpenReadStream();
        var uploadParameters = new ImageUploadParams
        {
            File = new FileDescription(image.FileName, imageStream)
        };

        if (folder != null)
            uploadParameters.Folder = folder;

        var result = await cloudinary.UploadAsync(uploadParameters, cancellationToken);

        if (result.Error != null)
        {
            throw new InvalidOperationException(
                $"Cloudinary SDK upload failed: {result.Error.Message}");
        }

        if (string.IsNullOrWhiteSpace(result.SecureUrl?.ToString()) ||
            string.IsNullOrWhiteSpace(result.PublicId))
        {
            throw new InvalidOperationException("Cloudinary returned an invalid upload response.");
        }

        return new UploadedImage(result.SecureUrl.ToString(), result.PublicId);
    }

    private static void ValidateImage(IFormFile image)
    {
        if (image.Length == 0)
            throw new InvalidOperationException("One of the uploaded images is empty.");

        if (image.Length > MaxImageBytes)
            throw new InvalidOperationException("Images must be 8 MB or smaller.");

        if (!AllowedContentTypes.Contains(image.ContentType))
            throw new InvalidOperationException("Only JPG, PNG, WEBP, and GIF images are supported.");
    }

}

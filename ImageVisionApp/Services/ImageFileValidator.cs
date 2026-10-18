using Microsoft.AspNetCore.Http;

namespace ImageVisionApp.Services
{
    /// <summary>
    /// Result of a file validation check.
    /// </summary>
    public record FileValidationResult(bool IsValid, string? ErrorMessage);

    public interface IImageFileValidator
    {
        FileValidationResult Validate(IFormFile? file);
    }

    /// <summary>
    /// Validates uploaded files before they ever reach blob storage or the
    /// Vision API — rejects wrong types/oversized files early and cheaply.
    /// </summary>
    public class ImageFileValidator : IImageFileValidator
    {
        private static readonly string[] AllowedContentTypes =
        {
            "image/jpeg", "image/png", "image/gif", "image/bmp", "image/webp"
        };

        private const long MaxFileSizeBytes = 4 * 1024 * 1024; // 4 MB

        public FileValidationResult Validate(IFormFile? file)
        {
            if (file is null || file.Length == 0)
            {
                return new FileValidationResult(false, "Please select an image to upload.");
            }

            if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return new FileValidationResult(false,
                    "Unsupported file type. Please upload a JPEG, PNG, GIF, BMP, or WEBP image.");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return new FileValidationResult(false,
                    $"File is too large. Maximum size is {MaxFileSizeBytes / (1024 * 1024)}MB.");
            }

            return new FileValidationResult(true, null);
        }
    }
}

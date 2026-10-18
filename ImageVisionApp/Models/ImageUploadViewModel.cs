using Microsoft.AspNetCore.Http;

namespace ImageVisionApp.Models
{
    /// <summary>
    /// Bound from the Home/Index upload form.
    /// </summary>
    public class ImageUploadViewModel
    {
        public IFormFile? ImageFile { get; set; }
    }

    /// <summary>
    /// Passed to the Result view — combines the analysis with the display image.
    /// </summary>
    public class ImageResultViewModel
    {
        public VisionAnalysisResult Analysis { get; set; } = new();
    }
}

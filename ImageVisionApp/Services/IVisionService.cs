using ImageVisionApp.Models;

namespace ImageVisionApp.Services
{
    /// <summary>
    /// Abstraction over the Azure Computer Vision AI API.
    /// </summary>
    public interface IVisionService
    {
        /// <summary>
        /// Sends the image (by URL) to Computer Vision and returns a
        /// caption + confidence + tags describing its contents.
        /// </summary>
        Task<VisionAnalysisResult> AnalyzeImageAsync(string imageUrl);
    }
}

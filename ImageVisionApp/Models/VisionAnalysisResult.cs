namespace ImageVisionApp.Models
{
    /// <summary>
    /// Represents the outcome of running an image through the Computer Vision API.
    /// Kept as a plain data model so it can be cached, serialized, and passed to views
    /// without any dependency on the Azure SDK types.
    /// </summary>
    public class VisionAnalysisResult
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool FromCache { get; set; }
    }
}

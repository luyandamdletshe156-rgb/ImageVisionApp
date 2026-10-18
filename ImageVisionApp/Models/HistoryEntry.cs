namespace ImageVisionApp.Models
{
    /// <summary>
    /// One row in the analysis history — created every time a photo is
    /// uploaded and analyzed, whether or not that analysis came from cache.
    /// Distinct from <see cref="VisionAnalysisResult"/> caching: the cache
    /// avoids repeat calls to the Vision API for identical bytes, while
    /// history is a log of every visit and needs its own identity so a
    /// single entry can be deleted without touching the others.
    /// </summary>
    public class HistoryEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool FromCache { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public static HistoryEntry FromAnalysis(VisionAnalysisResult analysis) => new()
        {
            ImageUrl = analysis.ImageUrl,
            Description = analysis.Description,
            ConfidenceScore = analysis.ConfidenceScore,
            Tags = analysis.Tags,
            FromCache = analysis.FromCache
        };
    }
}

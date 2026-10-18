using System.Text;
using System.Text.Json;
using ImageVisionApp.Models;

namespace ImageVisionApp.Services
{
    /// <summary>
    /// Calls the Computer Vision v3.2 "Describe Image" REST endpoint directly.
    /// v3.2 is used instead of the 4.0 SDK's Caption feature because 4.0
    /// captioning is only available in a handful of Azure regions
    /// (East US, West Europe, etc.) — v3.2 works in every Vision region,
    /// including South Africa North.
    /// </summary>
    public class VisionService : IVisionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VisionService> _logger;

        public VisionService(IHttpClientFactory httpClientFactory, ILogger<VisionService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ComputerVision");
            _logger = logger;
        }

        public async Task<VisionAnalysisResult> AnalyzeImageAsync(string imageUrl)
        {
            _logger.LogInformation("Calling Computer Vision v3.2 describe endpoint for {ImageUrl}", imageUrl);

            var requestBody = JsonSerializer.Serialize(new { url = imageUrl });
            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // BaseAddress + auth header are configured centrally in Program.cs
            // via the named "ComputerVision" HttpClient.
            using var response = await _httpClient.PostAsync(
                "vision/v3.2/describe?maxCandidates=1&language=en",
                content);

            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Computer Vision API error {Status}: {Body}", response.StatusCode, responseJson);
                return new VisionAnalysisResult
                {
                    ImageUrl = imageUrl,
                    Description = "Analysis failed — the Vision API returned an error.",
                    ConfidenceScore = 0,
                    Tags = new List<string>()
                };
            }

            using var doc = JsonDocument.Parse(responseJson);
            var description = doc.RootElement.GetProperty("description");

            var captions = description.GetProperty("captions");
            string captionText = "No description available.";
            double confidence = 0;

            if (captions.GetArrayLength() > 0)
            {
                var firstCaption = captions[0];
                captionText = firstCaption.GetProperty("text").GetString() ?? captionText;
                confidence = firstCaption.GetProperty("confidence").GetDouble();
            }

            var tags = description.GetProperty("tags")
                .EnumerateArray()
                .Select(t => t.GetString() ?? string.Empty)
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            return new VisionAnalysisResult
            {
                ImageUrl = imageUrl,
                Description = captionText,
                ConfidenceScore = confidence,
                Tags = tags,
                FromCache = false
            };
        }
    }
}

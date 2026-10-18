using ImageVisionApp.Models;
using ImageVisionApp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ImageVisionApp.Tests
{
    public class HistoryServiceTests
    {
        private static HistoryService CreateService() => new(Mock.Of<ILogger<HistoryService>>());

        private static VisionAnalysisResult MakeAnalysis(string description = "a photo") => new()
        {
            ImageUrl = "https://fake.blob.core.windows.net/uploaded-images/x.jpg",
            Description = description,
            ConfidenceScore = 0.8,
            Tags = new List<string> { "tag" }
        };

        [Fact]
        public void Add_AssignsIdAndStoresEntry()
        {
            var service = CreateService();

            var entry = service.Add(MakeAnalysis());

            Assert.NotEqual(Guid.Empty, entry.Id);
            Assert.Single(service.GetAll());
        }

        [Fact]
        public void GetAll_ReturnsMostRecentFirst()
        {
            var service = CreateService();

            var first = service.Add(MakeAnalysis("first"));
            Thread.Sleep(5);
            var second = service.Add(MakeAnalysis("second"));

            var all = service.GetAll();

            Assert.Equal(second.Id, all[0].Id);
            Assert.Equal(first.Id, all[1].Id);
        }

        [Fact]
        public void Remove_DeletesOnlyTheMatchingEntry()
        {
            var service = CreateService();
            var keep = service.Add(MakeAnalysis("keep"));
            var delete = service.Add(MakeAnalysis("delete"));

            var removed = service.Remove(delete.Id);

            Assert.True(removed);
            var remaining = service.GetAll();
            Assert.Single(remaining);
            Assert.Equal(keep.Id, remaining[0].Id);
        }

        [Fact]
        public void Remove_UnknownId_ReturnsFalse()
        {
            var service = CreateService();

            var removed = service.Remove(Guid.NewGuid());

            Assert.False(removed);
        }

        [Fact]
        public void Clear_RemovesEverything()
        {
            var service = CreateService();
            service.Add(MakeAnalysis());
            service.Add(MakeAnalysis());

            service.Clear();

            Assert.Empty(service.GetAll());
        }
    }
}

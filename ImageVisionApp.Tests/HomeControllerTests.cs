using ImageVisionApp.Controllers;
using ImageVisionApp.Models;
using ImageVisionApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ImageVisionApp.Tests
{
    /// <summary>
    /// These tests never touch Azure — HomeController only knows about
    /// interfaces (IBlobStorageService, IVisionService, ICacheService,
    /// IImageFileValidator), so every dependency here is a Moq fake.
    /// That's the practical payoff of dependency inversion: the controller's
    /// logic is testable in isolation, with no network calls and no real
    /// storage account required.
    /// </summary>
    public class HomeControllerTests
    {
        private readonly Mock<IBlobStorageService> _blobStorageMock = new();
        private readonly Mock<IVisionService> _visionMock = new();
        private readonly Mock<ICacheService> _cacheMock = new();
        private readonly Mock<IHistoryService> _historyMock = new();
        private readonly Mock<IImageFileValidator> _validatorMock = new();
        private readonly Mock<ILogger<HomeController>> _loggerMock = new();

        private HomeController CreateController() =>
            new(_blobStorageMock.Object, _visionMock.Object, _cacheMock.Object, _historyMock.Object, _validatorMock.Object, _loggerMock.Object);

        private static IFormFile CreateFakeFile(byte[] bytes, string contentType = "image/jpeg", string fileName = "photo.jpg")
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(bytes.Length);
            // Each call to OpenReadStream must return a fresh, unconsumed stream —
            // the real controller opens the stream twice (once to hash, once to upload).
            mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(bytes));
            return mockFile.Object;
        }

        [Fact]
        public void Index_ReturnsViewWithEmptyModel()
        {
            var controller = CreateController();

            var result = controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<ImageUploadViewModel>(viewResult.Model);
        }

        [Fact]
        public async Task Upload_InvalidFile_ReturnsIndexViewWithModelError()
        {
            _validatorMock
                .Setup(v => v.Validate(It.IsAny<IFormFile>()))
                .Returns(new FileValidationResult(false, "Unsupported file type."));

            var controller = CreateController();
            var model = new ImageUploadViewModel { ImageFile = CreateFakeFile(new byte[] { 1, 2, 3 }) };

            var result = await controller.Upload(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(controller.ModelState.IsValid);

            // Nothing downstream should ever run for an invalid file.
            _blobStorageMock.Verify(b => b.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _visionMock.Verify(v => v.AnalyzeImageAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Upload_CacheHit_SkipsBlobStorageAndVisionApi()
        {
            _validatorMock
                .Setup(v => v.Validate(It.IsAny<IFormFile>()))
                .Returns(new FileValidationResult(true, null));

            var cachedResult = new VisionAnalysisResult
            {
                ImageUrl = "https://fake.blob.core.windows.net/uploaded-images/cached.jpg",
                Description = "a cached description",
                ConfidenceScore = 0.9,
                Tags = new List<string> { "cached", "tag" }
            };

            VisionAnalysisResult? outResult = cachedResult;
            _cacheMock
                .Setup(c => c.TryGet(It.IsAny<string>(), out outResult))
                .Returns(true);

            var controller = CreateController();
            var model = new ImageUploadViewModel { ImageFile = CreateFakeFile(new byte[] { 1, 2, 3 }) };

            var result = await controller.Upload(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Result", viewResult.ViewName);
            var vm = Assert.IsType<ImageResultViewModel>(viewResult.Model);

            Assert.True(vm.Analysis.FromCache);
            Assert.Equal("a cached description", vm.Analysis.Description);

            // The whole point of caching: neither of these should be called on a hit.
            _blobStorageMock.Verify(b => b.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _visionMock.Verify(v => v.AnalyzeImageAsync(It.IsAny<string>()), Times.Never);

            // History logs every visit, even ones served from cache.
            _historyMock.Verify(h => h.Add(It.IsAny<VisionAnalysisResult>()), Times.Once);
        }

        [Fact]
        public async Task Upload_CacheMiss_CallsBlobStorageThenVisionThenCachesResult()
        {
            _validatorMock
                .Setup(v => v.Validate(It.IsAny<IFormFile>()))
                .Returns(new FileValidationResult(true, null));

            VisionAnalysisResult? outResult = null;
            _cacheMock
                .Setup(c => c.TryGet(It.IsAny<string>(), out outResult))
                .Returns(false);

            _blobStorageMock
                .Setup(b => b.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("https://fake.blob.core.windows.net/uploaded-images/new.jpg");

            _visionMock
                .Setup(v => v.AnalyzeImageAsync("https://fake.blob.core.windows.net/uploaded-images/new.jpg"))
                .ReturnsAsync(new VisionAnalysisResult
                {
                    ImageUrl = "https://fake.blob.core.windows.net/uploaded-images/new.jpg",
                    Description = "a fresh description",
                    ConfidenceScore = 0.85,
                    Tags = new List<string> { "fresh" }
                });

            var controller = CreateController();
            var model = new ImageUploadViewModel { ImageFile = CreateFakeFile(new byte[] { 4, 5, 6 }) };

            var result = await controller.Upload(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Result", viewResult.ViewName);
            var vm = Assert.IsType<ImageResultViewModel>(viewResult.Model);

            Assert.False(vm.Analysis.FromCache);
            Assert.Equal("a fresh description", vm.Analysis.Description);

            // On a miss, both real dependencies get called exactly once, in order,
            // and the result is written back into the cache for next time.
            _blobStorageMock.Verify(b => b.UploadImageAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg"), Times.Once);
            _visionMock.Verify(v => v.AnalyzeImageAsync("https://fake.blob.core.windows.net/uploaded-images/new.jpg"), Times.Once);
            _cacheMock.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<VisionAnalysisResult>()), Times.Once);
            _historyMock.Verify(h => h.Add(It.IsAny<VisionAnalysisResult>()), Times.Once);
        }

        [Fact]
        public void History_ReturnsViewWithEntriesFromService()
        {
            var entries = new List<HistoryEntry> { new() { Description = "a dog" } };
            _historyMock.Setup(h => h.GetAll()).Returns(entries);

            var controller = CreateController();
            var result = controller.History();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(entries, viewResult.Model);
        }

        [Fact]
        public void DeleteHistoryEntry_RemovesEntryAndRedirectsToHistory()
        {
            var id = Guid.NewGuid();
            var controller = CreateController();

            var result = controller.DeleteHistoryEntry(id);

            _historyMock.Verify(h => h.Remove(id), Times.Once);
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(HomeController.History), redirect.ActionName);
        }

        [Fact]
        public void ClearHistory_ClearsServiceAndRedirectsToHistory()
        {
            var controller = CreateController();

            var result = controller.ClearHistory();

            _historyMock.Verify(h => h.Clear(), Times.Once);
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(HomeController.History), redirect.ActionName);
        }

        [Fact]
        public void About_ReturnsView()
        {
            var controller = CreateController();

            var result = controller.About();

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Upload_SameFileContentTwice_ProducesSameCacheKey()
        {
            // Proves the cache key is derived from file bytes, not filename —
            // two IFormFile instances with identical content but different
            // names should hit the same cache entry.
            _validatorMock
                .Setup(v => v.Validate(It.IsAny<IFormFile>()))
                .Returns(new FileValidationResult(true, null));

            VisionAnalysisResult? outResult = null;
            _cacheMock
                .Setup(c => c.TryGet(It.IsAny<string>(), out outResult))
                .Returns(false);

            _blobStorageMock
                .Setup(b => b.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("https://fake.blob.core.windows.net/uploaded-images/x.jpg");

            _visionMock
                .Setup(v => v.AnalyzeImageAsync(It.IsAny<string>()))
                .ReturnsAsync(new VisionAnalysisResult { Description = "x" });

            var sameBytes = new byte[] { 9, 9, 9, 9 };
            var controller = CreateController();

            string? capturedKey1 = null, capturedKey2 = null;
            _cacheMock
                .Setup(c => c.Set(It.IsAny<string>(), It.IsAny<VisionAnalysisResult>()))
                .Callback<string, VisionAnalysisResult>((key, _) =>
                {
                    if (capturedKey1 is null) capturedKey1 = key;
                    else capturedKey2 = key;
                });

            await controller.Upload(new ImageUploadViewModel { ImageFile = CreateFakeFile(sameBytes, fileName: "first-name.jpg") });
            await controller.Upload(new ImageUploadViewModel { ImageFile = CreateFakeFile(sameBytes, fileName: "totally-different-name.png") });

            Assert.NotNull(capturedKey1);
            Assert.Equal(capturedKey1, capturedKey2);
        }
    }
}

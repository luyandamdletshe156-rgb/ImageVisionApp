using ImageVisionApp.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace ImageVisionApp.Tests
{
    public class ImageFileValidatorTests
    {
        private readonly ImageFileValidator _validator = new();

        private static IFormFile CreateFakeFile(string contentType, long length, string fileName = "test.jpg")
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Length).Returns(length);
            mockFile.Setup(f => f.FileName).Returns(fileName);
            return mockFile.Object;
        }

        [Fact]
        public void Validate_NullFile_ReturnsInvalid()
        {
            var result = _validator.Validate(null);

            Assert.False(result.IsValid);
            Assert.Equal("Please select an image to upload.", result.ErrorMessage);
        }

        [Fact]
        public void Validate_EmptyFile_ReturnsInvalid()
        {
            var file = CreateFakeFile("image/jpeg", 0);

            var result = _validator.Validate(file);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("image/jpeg")]
        [InlineData("image/png")]
        [InlineData("image/gif")]
        [InlineData("image/bmp")]
        [InlineData("image/webp")]
        public void Validate_AllowedContentTypes_ReturnsValid(string contentType)
        {
            var file = CreateFakeFile(contentType, 1024);

            var result = _validator.Validate(file);

            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
        }

        [Theory]
        [InlineData("application/pdf")]
        [InlineData("text/plain")]
        [InlineData("video/mp4")]
        public void Validate_DisallowedContentTypes_ReturnsInvalid(string contentType)
        {
            var file = CreateFakeFile(contentType, 1024);

            var result = _validator.Validate(file);

            Assert.False(result.IsValid);
            Assert.Contains("Unsupported file type", result.ErrorMessage);
        }

        [Fact]
        public void Validate_FileOverSizeLimit_ReturnsInvalid()
        {
            var oversized = (5 * 1024 * 1024L); // 5MB, over the 4MB limit
            var file = CreateFakeFile("image/jpeg", oversized);

            var result = _validator.Validate(file);

            Assert.False(result.IsValid);
            Assert.Contains("too large", result.ErrorMessage);
        }

        [Fact]
        public void Validate_FileExactlyAtSizeLimit_ReturnsValid()
        {
            var exactly4Mb = 4 * 1024 * 1024L;
            var file = CreateFakeFile("image/jpeg", exactly4Mb);

            var result = _validator.Validate(file);

            Assert.True(result.IsValid);
        }
    }
}

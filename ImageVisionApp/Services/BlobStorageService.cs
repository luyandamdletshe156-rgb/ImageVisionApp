using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ImageVisionApp.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(IConfiguration config, ILogger<BlobStorageService> logger)
        {
            _logger = logger;

            var connectionString = config["BlobStorage:ConnectionString"]
                ?? throw new InvalidOperationException("BlobStorage:ConnectionString is not configured.");
            var containerName = config["BlobStorage:ContainerName"]
                ?? throw new InvalidOperationException("BlobStorage:ContainerName is not configured.");

            var serviceClient = new BlobServiceClient(connectionString);
            _containerClient = serviceClient.GetBlobContainerClient(containerName);

            // Ensure the container exists and allows public read on blobs so
            // the browser can render the uploaded image directly from its URL.
            _containerClient.CreateIfNotExists(PublicAccessType.Blob);
        }

        public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType)
        {
            // Prefix with a GUID so concurrent uploads never collide.
            var blobName = $"{Guid.NewGuid()}_{fileName}";
            var blobClient = _containerClient.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders { ContentType = contentType };
            await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = headers });

            _logger.LogInformation("Uploaded {BlobName} to blob storage.", blobName);

            return blobClient.Uri.ToString();
        }
    }
}

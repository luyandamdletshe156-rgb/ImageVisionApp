namespace ImageVisionApp.Services
{
    /// <summary>
    /// Abstraction over Azure Blob Storage so the rest of the app never
    /// touches the Azure SDK directly. Makes it trivial to swap storage
    /// providers or mock in tests.
    /// </summary>
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads a file stream to blob storage and returns its public URL.
        /// </summary>
        Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType);
    }
}

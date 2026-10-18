using ImageVisionApp.Models;

namespace ImageVisionApp.Services
{
    /// <summary>
    /// Abstraction over the analysis history store. Kept separate from
    /// ICacheService — the cache exists to skip repeat Vision API calls,
    /// history exists so a user can review and delete past results.
    /// </summary>
    public interface IHistoryService
    {
        /// <summary>Adds an entry and returns it (with its assigned Id).</summary>
        HistoryEntry Add(VisionAnalysisResult analysis);

        /// <summary>All entries, most recent first.</summary>
        IReadOnlyList<HistoryEntry> GetAll();

        /// <summary>Removes a single entry. Returns false if the Id wasn't found.</summary>
        bool Remove(Guid id);

        /// <summary>Removes every entry.</summary>
        void Clear();
    }
}

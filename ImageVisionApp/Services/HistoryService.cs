using System.Collections.Concurrent;
using ImageVisionApp.Models;

namespace ImageVisionApp.Services
{
    /// <summary>
    /// In-memory history store. Registered as a singleton (see Program.cs)
    /// so entries persist across requests for the lifetime of the process —
    /// this mirrors the existing IMemoryCache-backed ICacheService and needs
    /// no database. Swap this for a database- or Table Storage-backed
    /// implementation later without touching the controller, since it only
    /// depends on IHistoryService.
    /// </summary>
    public class HistoryService : IHistoryService
    {
        private readonly ConcurrentDictionary<Guid, HistoryEntry> _entries = new();
        private readonly ILogger<HistoryService> _logger;

        public HistoryService(ILogger<HistoryService> logger)
        {
            _logger = logger;
        }

        public HistoryEntry Add(VisionAnalysisResult analysis)
        {
            var entry = HistoryEntry.FromAnalysis(analysis);
            _entries[entry.Id] = entry;
            _logger.LogInformation("History entry {Id} added.", entry.Id);
            return entry;
        }

        public IReadOnlyList<HistoryEntry> GetAll() =>
            _entries.Values
                .OrderByDescending(e => e.CreatedAtUtc)
                .ToList();

        public bool Remove(Guid id)
        {
            var removed = _entries.TryRemove(id, out _);
            if (removed)
            {
                _logger.LogInformation("History entry {Id} deleted.", id);
            }
            else
            {
                _logger.LogWarning("Delete requested for unknown history entry {Id}.", id);
            }
            return removed;
        }

        public void Clear()
        {
            var count = _entries.Count;
            _entries.Clear();
            _logger.LogInformation("History cleared ({Count} entries removed).", count);
        }
    }
}

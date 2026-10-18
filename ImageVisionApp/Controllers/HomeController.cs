using System.Security.Cryptography;
using ImageVisionApp.Models;
using ImageVisionApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageVisionApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly IVisionService _visionService;
        private readonly ICacheService _cacheService;
        private readonly IHistoryService _historyService;
        private readonly IImageFileValidator _fileValidator;
        private readonly ILogger<HomeController> _logger;

        // Constructor injection — controller has zero knowledge of Azure SDKs,
        // it only talks to the service abstractions.
        public HomeController(
            IBlobStorageService blobStorageService,
            IVisionService visionService,
            ICacheService cacheService,
            IHistoryService historyService,
            IImageFileValidator fileValidator,
            ILogger<HomeController> logger)
        {
            _blobStorageService = blobStorageService;
            _visionService = visionService;
            _cacheService = cacheService;
            _historyService = historyService;
            _fileValidator = fileValidator;
            _logger = logger;
        }

        // The landing page — a lightweight welcome/marketing screen with a
        // logo illustration and a CTA into the actual upload flow.
        [HttpGet]
        public IActionResult Home() => View();

        // The upload screen — a single large drop zone, no separate
        // marketing page to click through first.
        [HttpGet]
        public IActionResult Index()
        {
            return View(new ImageUploadViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(ImageUploadViewModel model)
        {
            var validation = _fileValidator.Validate(model.ImageFile);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Upload rejected: {Reason}", validation.ErrorMessage);
                ModelState.AddModelError(string.Empty, validation.ErrorMessage!);
                return View("Index", model);
            }

            var file = model.ImageFile!; // validator guarantees non-null when IsValid

            // Hash the file content so identical uploads share one cache entry,
            // regardless of filename.
            string cacheKey;
            using (var sha = SHA256.Create())
            using (var hashStream = file.OpenReadStream())
            {
                var hashBytes = await sha.ComputeHashAsync(hashStream);
                cacheKey = Convert.ToHexString(hashBytes);
            }

            VisionAnalysisResult analysis;

            if (_cacheService.TryGet(cacheKey, out VisionAnalysisResult? cached) && cached is not null)
            {
                cached.FromCache = true;
                analysis = cached;
            }
            else
            {
                // Not cached — upload to blob storage, then analyze.
                await using var uploadStream = file.OpenReadStream();
                var imageUrl = await _blobStorageService.UploadImageAsync(
                    uploadStream,
                    file.FileName,
                    file.ContentType);

                analysis = await _visionService.AnalyzeImageAsync(imageUrl);
                _cacheService.Set(cacheKey, analysis);
            }

            // Every visit gets its own history entry, cache hit or not — the
            // cache dedupes Vision API calls, history logs what the user did.
            _historyService.Add(analysis);

            return View("Result", new ImageResultViewModel { Analysis = analysis });
        }

        [HttpGet]
        public IActionResult History()
        {
            var entries = _historyService.GetAll();
            return View(entries);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteHistoryEntry(Guid id)
        {
            _historyService.Remove(id);
            return RedirectToAction(nameof(History));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearHistory()
        {
            _historyService.Clear();
            return RedirectToAction(nameof(History));
        }

        [HttpGet]
        public IActionResult About() => View();

        public IActionResult Error() => View();
    }
}
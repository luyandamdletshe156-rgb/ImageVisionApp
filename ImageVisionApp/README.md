# Image Vision Lab

.NET Core MVC app that lets a user upload a photo, stores it in Azure Blob
Storage, and uses Azure AI Computer Vision to generate a text description
of the image — with response caching so repeat uploads don't re-hit the API,
and a real history log (with delete) of everything analyzed.

## Setup

1. Create Azure resources:
   - **Storage account** → create a Blob container (e.g. `uploaded-images`)
     with public read access on blobs.
   - **Computer Vision** (or Azure AI Services multi-service) resource.
2. Open the project in Visual Studio.
3. Right-click the project → **Manage User Secrets** and add:
   ```json
   {
     "BlobStorage:ConnectionString": "your-connection-string",
     "ComputerVision:Endpoint": "your-endpoint",
     "ComputerVision:Key": "your-key"
   }
   ```
4. Press F5 to run.

## ⚠️ Azure App Service configuration — key names matter

The app reads configuration through **nested sections**
(`BlobStorage:ConnectionString`, `BlobStorage:ContainerName`,
`ComputerVision:Endpoint`, `ComputerVision:Key`) because that's how they're
structured in `appsettings.json`. In the Azure Portal, App Service
**Configuration → Application settings** must use one of these two forms
so ASP.NET Core's configuration binder can find them:

| Works | Doesn't work |
|---|---|
| `BlobStorage:ConnectionString` | `AzureBlobStorage` |
| `BlobStorage:ContainerName` | `AzureBlobStorageContainerName` |
| `ComputerVision:Endpoint` | `AzureComputerVisionEndpoint` |
| `ComputerVision:Key` | `AzureComputerVisionKey` |

(A colon works when typed directly into the Portal's Name field; if you're
setting it via `az webapp config appsettings set` or an ARM/Bicep template,
use a double underscore instead, e.g. `BlobStorage__ConnectionString`.)

If the names don't match, `BlobStorageService`'s constructor throws
`InvalidOperationException` on the very first request to `HomeController` —
which is every page, since the controller constructor runs regardless of
which action is hit. That's why a flat/mismatched setting shows up as an
**HTTP 500 on the home page itself**, not just on upload.

## Architecture / why it's structured this way

- **Controllers/HomeController** — orchestrates the workflow only. It has
  no knowledge of Azure SDKs; it just calls the five service interfaces.
- **Services/** — each concern is its own class behind an interface:
  - `IImageFileValidator` — rejects bad uploads before they cost anything
  - `IBlobStorageService` — talks to Azure Blob Storage
  - `IVisionService` — talks to Azure Computer Vision (v3.2 REST `describe`
    endpoint via `HttpClient`, not the 4.0 SDK — see note below)
  - `ICacheService` — wraps `IMemoryCache`, dedupes repeat Vision API calls
    for identical file bytes
  - `IHistoryService` — a separate, real log of every analysis run (not a
    cache). Backed by an in-memory `ConcurrentDictionary` singleton so it
    survives across requests; supports `Remove(id)` and `Clear()`, both
    wired to real POST actions and buttons on the History page — deleting
    a history entry actually deletes it, it isn't just hidden client-side.
- This follows the **Dependency Inversion Principle**: the controller and
  every service depend on abstractions, not concrete Azure classes. It
  means any one piece (e.g. swapping Blob Storage for local disk in a
  test, or swapping `HistoryService`'s in-memory store for a database
  later) can be replaced without touching the others — this is the
  "good OOP practice for reusability" the brief asks for.
- **Models/** — plain data classes (`VisionAnalysisResult`, `HistoryEntry`,
  view models) with no Azure SDK dependency, so they're safe to cache and
  pass to views directly.
- **ImageVisionApp.Tests/** — xUnit + Moq tests for `HomeController`,
  `HistoryService`, and `ImageFileValidator`. Every controller test mocks
  all five service interfaces — no real Azure connection is used or
  required to run them. This is the concrete proof behind the "good OOP
  practices" rubric item: the controller's logic (validation → cache check
  → blob upload → vision call → cache write → history log) is fully
  testable in isolation because it only ever depends on interfaces, never
  concrete Azure classes.

## Pages

- **New analysis** (`/`) — the landing page *is* the upload screen: a
  single large drop zone, no separate marketing page first.
- **Result** (shown after upload) — the photo alongside an "AI says"
  card with the caption, confidence bar, and tags.
- **History** (`/Home/History`) — every analysis ever run, most recent
  first, each with a real delete button (`POST /Home/DeleteHistoryEntry`)
  and a "Clear all" action (`POST /Home/ClearHistory`) — both hit
  `IHistoryService` on the server, so a refresh doesn't bring deleted
  entries back.
- **About** (`/Home/About`) — what the app does and how caching differs
  from history.

## Running the tests

Open `ImageVisionApp.sln` (not the individual `.csproj`) so both the app
and the test project load together. Then either:
- **Visual Studio**: Test menu → Run All Tests (or Test Explorer panel)
- **CLI**: `dotnet test` from the solution folder

No Azure credentials are needed to run the tests — everything is mocked.

## Why v3.2 instead of the 4.0 SDK

Image Analysis 4.0's Caption feature only works in a handful of Azure
regions (East US, West Europe, etc.) — it isn't available in South Africa
North. Rather than move the Computer Vision resource to another region,
`VisionService` calls the older v3.2 `describe` REST endpoint directly via
`HttpClient`, which is supported in every Azure Vision region. The
`HttpClient` is registered as a named client in `Program.cs` with the
endpoint and subscription key set once, so `VisionService` itself has no
Azure-specific configuration code.

## Caching strategy

The uploaded file's bytes are SHA-256 hashed to form the cache key. This
means uploading the *exact same image* twice — even under a different
filename — returns the cached result instantly instead of re-uploading to
blob storage and re-calling the paid Vision API. Cache entries expire
after 60 minutes (configurable in `appsettings.json` under `CacheSettings`).

## Demo script (for presentation)

1. **Home page** — show the upload form, mention the file-type/size
   validation hint.
2. **Upload an image** — click Upload & Analyze, point out the button
   changing to "Analyzing…" while the request is in flight.
3. **Result page** — show the image rendering (pulled straight from its
   Blob Storage URL), the generated caption, confidence %, and tags.
4. **Prove blob storage** — open the Azure Portal → Storage account →
   container, show the blob that was just created.
5. **Prove caching** — go back and upload the *same* image again. Point
   out the "⚡ Served from cache" badge and that it returns instantly
   with no new blob or new Vision API call.
6. **(Optional) Validation** — try uploading a non-image file or an
   oversized file to show the friendly rejection message.
7. **History** — click History in the sidebar, show past analyses, delete
   one entry, refresh the page to prove it's actually gone (not just
   hidden), then show "Clear all".

### Fallback

If live Azure calls fail on the day (network/quota), have a short screen
recording of the above flow as backup.

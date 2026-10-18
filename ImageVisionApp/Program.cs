using ImageVisionApp.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// In-memory cache (backing store for ICacheService)
builder.Services.AddMemoryCache();

// Named HttpClient for the Computer Vision v3.2 REST API — base address
// and the subscription key header are set once here so VisionService
// doesn't touch configuration or auth directly.
//
// NOTE ON AZURE APP SERVICE CONFIG: these keys must exist in App Service
// as "BlobStorage:ConnectionString", "BlobStorage:ContainerName",
// "ComputerVision:Endpoint" and "ComputerVision:Key" (colon-separated,
// matching appsettings.json's nested sections) — or as environment
// variables using a double underscore instead of a colon, e.g.
// "BlobStorage__ConnectionString". Flat names like "AzureBlobStorage" or
// "AzureComputerVisionKey" will NOT bind to these sections, and every
// request will 500 because these throw during startup/first request.
builder.Services.AddHttpClient("ComputerVision", client =>
{
    var endpoint = builder.Configuration["ComputerVision:Endpoint"]
        ?? throw new InvalidOperationException("ComputerVision:Endpoint is not configured.");
    var key = builder.Configuration["ComputerVision:Key"]
        ?? throw new InvalidOperationException("ComputerVision:Key is not configured.");

    client.BaseAddress = new Uri(endpoint);
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
});

// Application services — registered against their interfaces so
// controllers depend only on abstractions (DIP), and each can be
// swapped or mocked independently.
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IVisionService, VisionService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IImageFileValidator, ImageFileValidator>();

// Singleton — history must survive across requests within the process,
// not be recreated per-scope like the services above.
builder.Services.AddSingleton<IHistoryService, HistoryService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

using DevLeads.Infrastructure;
using DevLeads.Web.Api;
using DevLeads.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API returns EF entities with navigation properties — ignore cycles, and emit enums as names.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// SQLite database lives alongside the app content root.
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "devleads.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
var connectionString = builder.Configuration.GetConnectionString("DevLeads")
                       ?? $"Data Source={dbPath}";

builder.Services.AddDevLeads(connectionString);
builder.Services.AddSingleton<DevLeads.Web.AppRestartService>();

var app = builder.Build();

// Create + seed the database on startup.
await app.Services.InitializeDevLeadsAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/favicon.ico", () => Results.Redirect("/favicon.svg"));
app.MapDevLeadsApi();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

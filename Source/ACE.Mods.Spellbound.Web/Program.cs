using System.Security.Claims;

using ACE.Database.Models.Auth;
using ACE.Database.Models.Shard;
using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Web.Components;
using ACE.Mods.Spellbound.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
        options.Cookie.Name = "spellbound.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

// All three DbContexts get registered the same way: pull host/port/user/password/db
// from the matching MySql:<Section> in appsettings, build a connection string,
// register the EF context with retry-on-failure. Registering with options here
// makes the contexts' default OnConfiguring no-op (IsConfigured == true), which is
// important for SpellboundContext — the mod's normal hosting path passes options
// in via DI from Settings.json; the web app just bypasses that and reads from its
// own appsettings instead. Two configs, one schema.
static string BuildMySqlConnString(IConfigurationSection section) =>
    $"server={section["Host"]};port={section["Port"]};user={section["Username"]};password={section["Password"]};database={section["Database"]};AllowUserVariables=true;UseAffectedRows=false";

var authConn = BuildMySqlConnString(builder.Configuration.GetSection("MySql:Authentication"));
builder.Services.AddDbContext<AuthDbContext>(opts =>
    opts.UseMySql(authConn, ServerVersion.AutoDetect(authConn), my => my.EnableRetryOnFailure(3)));

var shardConn = BuildMySqlConnString(builder.Configuration.GetSection("MySql:Shard"));
builder.Services.AddDbContext<ShardDbContext>(opts =>
    opts.UseMySql(shardConn, ServerVersion.AutoDetect(shardConn), my => my.EnableRetryOnFailure(3)));

var spellboundConn = BuildMySqlConnString(builder.Configuration.GetSection("MySql:Spellbound"));
builder.Services.AddDbContext<SpellboundContext>(opts =>
    opts.UseMySql(spellboundConn, ServerVersion.AutoDetect(spellboundConn), my => my.EnableRetryOnFailure(3)));

builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<CharacterLookupService>();
builder.Services.AddScoped<WhoService>();
builder.Services.AddScoped<CharacterProfileService>();
builder.Services.AddScoped<LeaderboardService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Item icons live in the dat-extract output (~20k textures keyed by hex
// IconId) — too bulky to commit into wwwroot/. Serve them straight out of
// the extractor's uncategorized/ bucket via a separate static-files mount
// at /img/ac/items. Configurable via appsettings:ItemIconsDirectory; the
// default resolves to <repo>/out/dat-extract/uncategorized when run from
// the project directory. If the path is missing (e.g. extractor never ran)
// we log once at startup and skip the mount; item-icon <img> tags
// onerror-hide gracefully.
var itemIconsPath = app.Configuration["ItemIconsDirectory"];
if (string.IsNullOrWhiteSpace(itemIconsPath))
    itemIconsPath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "out", "dat-extract", "uncategorized");
itemIconsPath = Path.GetFullPath(itemIconsPath);

if (Directory.Exists(itemIconsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(itemIconsPath),
        RequestPath = "/img/ac/items"
    });
    app.Logger.LogInformation("Item icons served from {Path} via /img/ac/items", itemIconsPath);
}
else
{
    app.Logger.LogWarning(
        "ItemIconsDirectory not found at {Path} — item icons will be hidden. " +
        "Run dotnet run --project Source/ACE.Mods.Spellbound.DatExtractor or " +
        "set ItemIconsDirectory in appsettings.json.", itemIconsPath);
}

app.UseAuthentication();

// Dev-only auto-login. The env gate is at registration time so this middleware
// CANNOT be in the pipeline outside Development. The DevAuth:Enabled toggle is
// re-read per request from IConfiguration, so flipping the flag in
// appsettings.Development.json takes effect without restarting the server.
if (app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        var cfg = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var devAuth = cfg.GetSection("DevAuth");

        if (devAuth.GetValue<bool>("Enabled") && ctx.User.Identity?.IsAuthenticated != true)
        {
            var accountId = devAuth.GetValue<uint>("AccountId", 1);
            var accountName = devAuth.GetValue<string>("AccountName") ?? "dev";
            var accessLevel = devAuth.GetValue<uint>("AccessLevel", 0);

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
                    new Claim(ClaimTypes.Name, accountName),
                    new Claim("access_level", accessLevel.ToString()),
                    new Claim("dev_auth", "true")
                },
                authenticationType: "DevAuth");
            ctx.User = new ClaimsPrincipal(identity);
        }

        await next();
    });
}

app.UseAntiforgery();
app.UseAuthorization();

app.MapPost("/auth/logout", async (HttpContext ctx, IAntiforgery anti) =>
{
    await anti.ValidateRequestAsync(ctx);
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

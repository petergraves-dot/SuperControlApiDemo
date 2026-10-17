using System.Text;
using petergraves.Features.SuperControlDemo;
using petergraves.Features.SuperControlDataExportDemo;
using petergraves.Features.SuperControlListingSiteDemo;
using petergraves.Features.SuperControlProperty;
using petergraves.Features.PropertyExplorer;
using petergraves.Integrations.SuperControl;

var builder = WebApplication.CreateBuilder(args);

// Optional developer-specific settings. This file is git-ignored and is not published.
builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true)
    .AddEnvironmentVariables();

if (args.Length > 0)
{
    builder.Configuration.AddCommandLine(args);
}

var canonicalBaseUrlValue = builder.Configuration["Site:CanonicalBaseUrl"]?.TrimEnd('/');
if (!Uri.TryCreate(canonicalBaseUrlValue, UriKind.Absolute, out var canonicalBaseUri)
    || canonicalBaseUri.Scheme != Uri.UriSchemeHttps
    || canonicalBaseUri.AbsolutePath != "/"
    || !string.IsNullOrEmpty(canonicalBaseUri.Query))
{
    throw new InvalidOperationException(
        "Site:CanonicalBaseUrl must be an absolute HTTPS origin without a path or query string.");
}
var canonicalBaseUrl = canonicalBaseUri.GetLeftPart(UriPartial.Authority);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
});
builder.Services
    .AddOptions<SuperControlOptions>()
    .Bind(builder.Configuration.GetSection(SuperControlOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
            && baseUri.Scheme == Uri.UriSchemeHttps,
        "SuperControl:BaseUrl must be an absolute HTTPS URL.")
    .ValidateOnStart();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISuperControlResponseCache, SuperControlResponseCache>();
builder.Services.AddScoped<JsonBodyAntiforgeryFilter>();
builder.Services.AddHttpClient<ISuperControlClient, SuperControlClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<SuperControlOptions>>()
        .Value;

    if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress))
    {
        httpClient.BaseAddress = baseAddress;
    }

    httpClient.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Do not forward SC-TOKEN through an HTTP redirect to another origin.
    AllowAutoRedirect = false
});
builder.Services.AddScoped<ISuperControlListingSiteService, SuperControlListingSiteService>();
builder.Services.AddScoped<ISuperControlListingSiteDemoViewModelFactory, SuperControlListingSiteDemoViewModelFactory>();
builder.Services.AddScoped<ISuperControlPropertyViewModelFactory, SuperControlPropertyViewModelFactory>();
builder.Services.AddScoped<IDataExportDemoViewModelFactory, DataExportDemoViewModelFactory>();
builder.Services.AddScoped<IPropertyExplorerViewModelFactory, PropertyExplorerViewModelFactory>();

var app = builder.Build();
var cookieAudit = new List<object>();
const int cookieAuditLimit = 200;
const string contentSecurityPolicy = "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'; script-src 'self' https://www.googletagmanager.com https://secure.supercontrol.co.uk https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; img-src 'self' https: data:; font-src 'self'; connect-src 'self' https://www.googletagmanager.com https://*.google-analytics.com https://api.supercontrol.co.uk https://secure.supercontrol.co.uk https://tile.openstreetmap.org; frame-src 'self' https://secure.supercontrol.co.uk";

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = contentSecurityPolicy;
    await next();
});
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.Headers.TryGetValue("Set-Cookie", out var setCookies)
                && setCookies.Count > 0)
            {
                lock (cookieAudit)
                {
                    foreach (var cookie in setCookies)
                    {
                        cookieAudit.Add(new
                        {
                            TimestampUtc = DateTime.UtcNow,
                            Path = context.Request.Path.Value ?? "/",
                            Cookie = cookie
                        });
                    }

                    var overflow = cookieAudit.Count - cookieAuditLimit;
                    if (overflow > 0)
                    {
                        cookieAudit.RemoveRange(0, overflow);
                    }
                }
            }

            return Task.CompletedTask;
        });

        await next();
    });
}

app.UseRouting();

app.Use(async (context, next) =>
{
    if (ShouldNoIndex(context.Request.Path))
    {
        context.Items["SeoNoIndex"] = true;
        context.Response.Headers["X-Robots-Tag"] = "noindex";
    }

    await next();
});

app.UseAuthorization();

app.MapGet("/sitemap.xml", () =>
{
    string[] paths =
    {
        "/",
        "/supercontrol-demo",
        "/supercontrol-listing-site-demo",
        "/supercontrol-property-explorer",
        "/supercontrol-data-export",
        "/supercontrol-data-export/properties",
        "/cookies"
    };

    var xml = new StringBuilder();
    xml.Append("""<?xml version="1.0" encoding="UTF-8"?>""");
    xml.Append(
        """<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

    foreach (var path in paths)
    {
        var absoluteUrl = $"{canonicalBaseUrl}{path}";
        xml.Append("<url>");
        xml.Append($"<loc>{absoluteUrl}</loc>");
        xml.Append("</url>");
    }

    xml.Append("</urlset>");
    return Results.Content(xml.ToString(), "application/xml", Encoding.UTF8);
});

app.MapGet("/robots.txt", () =>
{
    var robots = $"User-agent: *\nAllow: /\nSitemap: {canonicalBaseUrl}/sitemap.xml\n";
    return Results.Text(robots, "text/plain", Encoding.UTF8);
});

if (app.Environment.IsDevelopment())
{
    app.MapGet("/_cookie-audit", () =>
    {
        lock (cookieAudit)
        {
            return Results.Json(cookieAudit);
        }
    });
}

app.MapControllers();

app.Run();

static bool ShouldNoIndex(PathString path)
{
    return path.StartsWithSegments("/supercontrol-listing-site-demo/property", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/supercontrol-property-explorer/search", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/supercontrol-listing-site-tests", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/supercontrol-data-export-tests", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase);
}

public partial class Program
{
}

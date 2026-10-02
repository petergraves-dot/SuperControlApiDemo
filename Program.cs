using System.Text;
using petergraves.Features.SuperControlDemo;
using petergraves.Features.SuperControlDataExportDemo;
using petergraves.Features.SuperControlListingSiteDemo;
using petergraves.Features.SuperControlProperty;
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

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
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

var app = builder.Build();
var cookieAudit = new List<object>();
const int cookieAuditLimit = 200;
const string contentSecurityPolicy = "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'; script-src 'self' https://secure.supercontrol.co.uk; style-src 'self' 'unsafe-inline'; img-src 'self' https: data:; font-src 'self'; connect-src 'self' https://api.supercontrol.co.uk https://secure.supercontrol.co.uk; frame-src 'self' https://secure.supercontrol.co.uk";

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
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }

    await next();
});

app.UseAuthorization();

app.MapGet("/sitemap.xml", (HttpRequest request) =>
{
    var scheme = request.Scheme;
    var host = request.Host.Value;
    var now = DateTime.UtcNow.ToString("yyyy-MM-dd");

    var urls = new[]
    {
        (Path: "/", ChangeFreq: "weekly", Priority: "1.0"),
        (Path: "/supercontrol-demo", ChangeFreq: "daily", Priority: "0.9"),
        (Path: "/supercontrol-listing-site-tutorial", ChangeFreq: "weekly", Priority: "0.7"),
        (Path: "/supercontrol-data-export", ChangeFreq: "weekly", Priority: "0.7"),
        (Path: "/supercontrol-data-export-tutorial", ChangeFreq: "weekly", Priority: "0.6")
    };

    var xml = new StringBuilder();
    xml.Append("""<?xml version="1.0" encoding="UTF-8"?>""");
    xml.Append(
        """<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

    foreach (var url in urls)
    {
        var absoluteUrl = $"{scheme}://{host}{url.Path}";
        xml.Append("<url>");
        xml.Append($"<loc>{absoluteUrl}</loc>");
        xml.Append($"<lastmod>{now}</lastmod>");
        xml.Append($"<changefreq>{url.ChangeFreq}</changefreq>");
        xml.Append($"<priority>{url.Priority}</priority>");
        xml.Append("</url>");
    }

    xml.Append("</urlset>");
    return Results.Content(xml.ToString(), "application/xml", Encoding.UTF8);
});

app.MapGet("/robots.txt", (HttpRequest request) =>
{
    var sitemapUrl = $"{request.Scheme}://{request.Host.Value}/sitemap.xml";
    var robots = $"User-agent: *\nDisallow: /supercontrol-listing-site-demo\nDisallow: /supercontrol-listing-site-demo/property/\nAllow: /\nSitemap: {sitemapUrl}\n";
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
    if (!path.HasValue)
    {
        return false;
    }

    return path.Value!.StartsWith("/supercontrol-listing-site-demo", StringComparison.OrdinalIgnoreCase)
        || path.Value.StartsWith("/supercontrol-data-export", StringComparison.OrdinalIgnoreCase);
}

public partial class Program
{
}

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using petergraves.Features.SuperControlDataExportDemo;
using petergraves.Features.SuperControlListingSiteDemo;
using petergraves.Features.SuperControlProperty;
using petergraves.Integrations.SuperControl;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services
    .AddOptions<SuperControlOptions>()
    .Bind(builder.Configuration.GetSection(SuperControlOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "SuperControl:BaseUrl must be an absolute URL.")
    .ValidateOnStart();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISuperControlResponseCache, SuperControlResponseCache>();
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
});
builder.Services.AddScoped<ISuperControlListingSiteService, SuperControlListingSiteService>();
builder.Services.AddScoped<ISuperControlListingSiteDemoViewModelFactory, SuperControlListingSiteDemoViewModelFactory>();
builder.Services.AddScoped<ISuperControlPropertyViewModelFactory, SuperControlPropertyViewModelFactory>();
builder.Services.AddScoped<IDataExportDemoViewModelFactory, DataExportDemoViewModelFactory>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("internal-refresh", limiterOptions =>
    {
        limiterOptions.PermitLimit = 6;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var app = builder.Build();
var cookieAudit = new List<object>();
const int cookieAuditLimit = 200;
const string contentSecurityPolicy = "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'; script-src 'self' https://cdn.jsdelivr.net https://secure.supercontrol.co.uk; style-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; img-src 'self' https: data:; font-src 'self' https: data:; connect-src 'self' https://api.supercontrol.co.uk https://secure.supercontrol.co.uk; frame-src 'self' https://secure.supercontrol.co.uk";

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
app.UseRateLimiter();

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

app.MapPost("/internal/supercontrol/cache-refresh", async (
    string? cadence,
    ISuperControlClient client,
    ISuperControlResponseCache responseCache,
    CancellationToken cancellationToken) =>
{
    var cadencePlan = ResolveCadence(cadence);
    if (cadencePlan is null)
    {
        return Results.BadRequest(new
        {
            error = "Invalid cadence. Use accounts, content-config, prices-availability, or all."
        });
    }

    var summary = new CacheRefreshSummary(cadencePlan.Name);

    var accountsIndex = await responseCache.GetOrFetchAsync(
        scope: "index",
        cacheKey: "properties/index",
        ttl: TimeSpan.FromHours(12),
        fetch: ct => client.GetAccountsIndexAsync(ct),
        cancellationToken: cancellationToken);

    AddStats(summary, "accounts-index", accountsIndex);

    if (!accountsIndex.Response.IsSuccess)
    {
        return Results.Json(summary, statusCode: StatusCodes.Status502BadGateway);
    }

    var parsedAccounts = TryDeserialize<AccountsIndexResponse>(accountsIndex.Response.Body);
    var accounts = parsedAccounts?.Accounts ?? [];
    summary.AccountCount = accounts.Count;

    if (cadencePlan.IncludeContentConfiguration)
    {
        foreach (var account in accounts)
        {
            await RefreshIndexAsync(
                summary,
                responseCache,
                client,
                "content-index",
                $"properties/contentindex/{account.AccountId}",
                TimeSpan.FromHours(6),
                cancellationToken);

            await RefreshIndexAsync(
                summary,
                responseCache,
                client,
                "configuration-index",
                $"properties/configurationindex/{account.AccountId}",
                TimeSpan.FromHours(6),
                cancellationToken);
        }
    }

    if (cadencePlan.IncludePricesAvailability)
    {
        foreach (var account in accounts)
        {
            await RefreshIndexAsync(
                summary,
                responseCache,
                client,
                "prices-index",
                $"properties/pricesindex/{account.AccountId}",
                TimeSpan.FromMinutes(30),
                cancellationToken);

            await RefreshIndexAsync(
                summary,
                responseCache,
                client,
                "availability-index",
                $"properties/availabilityindex/{account.AccountId}",
                TimeSpan.FromMinutes(30),
                cancellationToken);
        }
    }

    summary.CompletedAtUtc = DateTime.UtcNow;
    return Results.Json(summary);
})
.RequireRateLimiting("internal-refresh");

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

static CadencePlan? ResolveCadence(string? cadence)
{
    var value = cadence?.Trim().ToLowerInvariant() ?? "all";
    return value switch
    {
        "accounts" => new CadencePlan("accounts", false, false),
        "content-config" => new CadencePlan("content-config", true, false),
        "prices-availability" => new CadencePlan("prices-availability", false, true),
        "all" => new CadencePlan("all", true, true),
        _ => null
    };
}

static T? TryDeserialize<T>(string json)
{
    try
    {
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch
    {
        return default;
    }
}

static async Task RefreshIndexAsync(
    CacheRefreshSummary summary,
    ISuperControlResponseCache responseCache,
    ISuperControlClient client,
    string indexName,
    string cacheKey,
    TimeSpan ttl,
    CancellationToken cancellationToken)
{
    var result = await responseCache.GetOrFetchAsync(
        scope: "index",
        cacheKey: cacheKey,
        ttl: ttl,
        fetch: ct => client.GetByRelativeUrlAsync(cacheKey, ct),
        cancellationToken: cancellationToken);

    AddStats(summary, indexName, result);
}

static void AddStats(
    CacheRefreshSummary summary,
    string indexName,
    CachedSuperControlResponse response)
{
    summary.Requests++;
    if (response.Response.IsSuccess)
    {
        summary.Successes++;
    }
    else
    {
        summary.Failures++;
    }

    if (response.CacheHit)
    {
        summary.CacheHits++;
    }
    else
    {
        summary.CacheMisses++;
    }

    if (response.StaleFallback)
    {
        summary.StaleFallbacks++;
    }

    summary.IndexBreakdown[indexName] = summary.IndexBreakdown.TryGetValue(indexName, out var existing)
        ? existing with
        {
            Requests = existing.Requests + 1,
            Successes = existing.Successes + (response.Response.IsSuccess ? 1 : 0),
            Failures = existing.Failures + (response.Response.IsSuccess ? 0 : 1),
            CacheHits = existing.CacheHits + (response.CacheHit ? 1 : 0),
            CacheMisses = existing.CacheMisses + (response.CacheHit ? 0 : 1),
            StaleFallbacks = existing.StaleFallbacks + (response.StaleFallback ? 1 : 0)
        }
        : new CacheRefreshIndexStats(
            Requests: 1,
            Successes: response.Response.IsSuccess ? 1 : 0,
            Failures: response.Response.IsSuccess ? 0 : 1,
            CacheHits: response.CacheHit ? 1 : 0,
            CacheMisses: response.CacheHit ? 0 : 1,
            StaleFallbacks: response.StaleFallback ? 1 : 0);
}

file sealed record CadencePlan(string Name, bool IncludeContentConfiguration, bool IncludePricesAvailability);

file sealed class CacheRefreshSummary
{
    public CacheRefreshSummary(string cadence)
    {
        Cadence = cadence;
        StartedAtUtc = DateTime.UtcNow;
    }

    public string Cadence { get; }

    public DateTime StartedAtUtc { get; }

    public DateTime? CompletedAtUtc { get; set; }

    public int AccountCount { get; set; }

    public int Requests { get; set; }

    public int Successes { get; set; }

    public int Failures { get; set; }

    public int CacheHits { get; set; }

    public int CacheMisses { get; set; }

    public int StaleFallbacks { get; set; }

    public Dictionary<string, CacheRefreshIndexStats> IndexBreakdown { get; } = new(StringComparer.OrdinalIgnoreCase);
}

file sealed record CacheRefreshIndexStats(
    int Requests,
    int Successes,
    int Failures,
    int CacheHits,
    int CacheMisses,
    int StaleFallbacks);

public partial class Program
{
}

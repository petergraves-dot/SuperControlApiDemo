using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml.Linq;
using CookieAudit;
using Microsoft.Playwright;

if (args.SequenceEqual(new[] { "--install" }))
    return Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });

if (args.Contains("--help"))
{
    Console.WriteLine("CookieAudit [--base-url https://demo.petergraves.co.uk] [--output .cookie-audit/latest.json] [--inventory Data/cookie-policy.json] [--settle-ms 2000] [--allow-analytics]");
    Console.WriteLine("Run from the repository root. --install installs Chromium. Exit 0: passed; 1: findings/incomplete; 2: setup failure.");
    return 0;
}

string Option(string name, string fallback)
{
    var index = Array.IndexOf(args, name);
    return index < 0 ? fallback : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"Missing value for {name}.");
}

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
var observations = new List<Observation>();
var findings = new List<Finding>();
var coverage = new List<object>();
var started = DateTimeOffset.UtcNow;
var allowAnalytics = args.Contains("--allow-analytics");
try
{
    var output = Path.GetFullPath(Option("--output", ".cookie-audit/latest.json"));
    var baseUri = new Uri(Option("--base-url", "https://demo.petergraves.co.uk"));
    var inventory = JsonSerializer.Deserialize<Inventory>(await File.ReadAllTextAsync(Option("--inventory", "Data/cookie-policy.json")), jsonOptions)
        ?? throw new InvalidOperationException("Cookie inventory is empty.");
    if (baseUri.Scheme != "https" && !baseUri.IsLoopback)
        throw new ArgumentException("Use HTTPS for remote scans.");
    var settleMs = int.Parse(Option("--settle-ms", "2000"));
    if (settleMs is < 0 or > 30000) throw new ArgumentException("--settle-ms must be between 0 and 30000.");

    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync();
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var sitemap = XDocument.Parse(await http.GetStringAsync(new Uri(baseUri, "/sitemap.xml")));
    var paths = sitemap.Descendants(ns + "loc").Select(loc => new Uri(loc.Value))
        .Where(uri => uri.GetLeftPart(UriPartial.Authority) == new Uri(inventory.Scope).GetLeftPart(UriPartial.Authority))
        .Select(uri => uri.PathAndQuery).Concat(inventory.ScanPaths ?? []).Prepend("/").Distinct().Order().ToArray();
    if (paths.Any(path => !path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal)))
        throw new InvalidOperationException("Scan paths must be relative to the target origin.");
    if (paths.Length < 2) throw new InvalidOperationException("Sitemap contains fewer than two in-scope pages.");

    foreach (var scenario in new[] { "no-choice", "rejected", "accepted", "withdrawn" })
    {
        Console.WriteLine($"Scanning {scenario}: {paths.Length} pages");
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        if (!allowAnalytics)
        {
            await context.RouteAsync("**/*", async route =>
            {
                var uri = new Uri(route.Request.Url);
                if (AuditRules.IsAnalyticsRequest(uri)) await route.AbortAsync();
                else await route.ContinueAsync();
            });
        }
        var requests = new ConcurrentQueue<(string Page, string Host)>();
        var currentPath = "/";
        context.Request += (_, request) =>
        {
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) && AuditRules.IsAnalyticsRequest(uri))
                requests.Enqueue((currentPath, uri.Host));
        };
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(10000);
        try
        {
            var initial = await page.GotoAsync(new Uri(baseUri, "/").AbsoluteUri);
            if (initial?.Ok != true) throw new InvalidOperationException("Homepage did not return a successful response.");
            await page.WaitForTimeoutAsync(settleMs);
            if (scenario != "no-choice")
            {
                var banner = page.Locator("#stcm-banner");
                if (!await banner.IsVisibleAsync())
                    throw new InvalidOperationException("Consent banner is absent; this consent scenario could not be exercised.");

                // Requests before the click belong to the no-consent state.
                foreach (var request in requests)
                    findings.Add(new("no-choice", request.Page, $"Analytics request before a choice: {request.Host}."));
                await Capture("no-choice", "/", page, context);
                requests.Clear();
                var accept = scenario is "accepted" or "withdrawn";
                await banner.GetByRole(AriaRole.Button, new() { Name = accept ? "Accept analytics cookies" : "Reject analytics cookies", Exact = true }).ClickAsync();
                await page.WaitForTimeoutAsync(settleMs);

                if (scenario == "withdrawn")
                {
                    // Confirm analytics really ran before testing its withdrawal.
                    if (allowAnalytics && !(await context.CookiesAsync()).Any(cookie => inventory.Items.Any(rule => rule.Category == "Analytics" && AuditRules.Match(new Observation(scenario, "/", "cookie", cookie.Name, cookie.Domain, cookie.Expires, cookie.Secure, cookie.HttpOnly, cookie.SameSite.ToString()), inventory) == rule)))
                        findings.Add(new(scenario, "/", "No analytics cookie after acceptance; withdrawal coverage is incomplete."));
                    await page.Locator("#stcm-icon").ClickAsync();
                    requests.Clear();
                    await page.Locator("#stcm-modal").GetByRole(AriaRole.Button, new() { Name = "Reject analytics cookies", Exact = true }).ClickAsync();
                    // Silktide reloads after script consent is revoked.
                    await page.WaitForTimeoutAsync(Math.Max(500, settleMs));
                    await page.WaitForLoadStateAsync(LoadState.Load);
                }
                else requests.Clear();
            }

            foreach (var path in paths)
            {
                currentPath = path;
                var response = await page.GotoAsync(new Uri(baseUri, path).AbsoluteUri);
                if (response?.Ok != true) throw new InvalidOperationException($"Page {path} did not return a successful response.");
                if (new Uri(page.Url).GetLeftPart(UriPartial.Authority) != baseUri.GetLeftPart(UriPartial.Authority))
                    throw new InvalidOperationException("Navigation left the scan origin.");
                await page.WaitForTimeoutAsync(settleMs);
                await Capture(scenario, path, page, context);

                coverage.Add(new { scenario, path, completed = true });
            }

            if (scenario != "accepted")
                foreach (var request in requests.Distinct())
                    findings.Add(new(scenario, request.Page, $"Analytics request attempted without consent: {request.Host}."));
            else if (allowAnalytics && !observations.Any(item => item.Scenario == scenario && AuditRules.Match(item, inventory)?.Category == "Analytics"))
                findings.Add(new(scenario, "/", "No analytics cookie observed after acceptance; analytics may be blocked or misconfigured."));
        }
        catch (Exception exception)
        {
            findings.Add(new(scenario, currentPath, $"Incomplete scan: {exception.GetType().Name}. {exception.Message.Split('\n')[0]}"));
            coverage.Add(new { scenario, path = currentPath, completed = false });
        }
    }

    foreach (var item in observations)
        findings.AddRange(AuditRules.Check(item, inventory, started));
    if (!allowAnalytics)
        findings.Add(new("accepted", "/", "Analytics requests were blocked by the scanner. Real analytics cookies and their withdrawal could not be verified."));
    var notObserved = inventory.Items.Where(rule => !observations.Any(item => AuditRules.Match(item, inventory) == rule))
        .Select(rule => new { rule.Name, rule.Storage }).ToArray();
    var report = new
    {
        startedUtc = started, completedUtc = DateTimeOffset.UtcNow, baseUrl = baseUri.GetLeftPart(UriPartial.Authority),
        passed = findings.Count == 0, analyticsRequestsBlocked = !allowAnalytics, settleMs, coverage,
        observations = observations.Distinct().OrderBy(item => item.Scenario).ThenBy(item => item.Page).ThenBy(item => item.Name),
        findings = findings.Distinct(), notObserved,
        limitations = new[] {
            "Point-in-time Chromium scan of same-origin sitemap pages. Does not follow external links or submit forms.",
            "Captures cookies and local/session storage in loaded frames. Does not inspect IndexedDB, Cache Storage or every possible interaction.",
            "Observation window is recorded in settleMs. Geolocation, browser and timing can change results.",
            "Cookie and storage values are excluded. Descriptions come from the reviewed inventory, not the scanner.",
            "Scan visits can appear in analytics. Configure an analytics exclusion for the scanner if required."
        }
    };
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, jsonOptions) + "\n");
    Console.WriteLine($"Saved {output}: {findings.Distinct().Count()} findings. Policy inventory was not changed.");
    return findings.Count == 0 ? 0 : 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Scan setup failed: {exception.GetType().Name}: {exception.Message.Split('\n')[0]}");
    return 2;
}

async Task Capture(string scenario, string path, IPage page, IBrowserContext context)
{
    foreach (var cookie in await context.CookiesAsync())
        observations.Add(new(scenario, path, "cookie", cookie.Name, cookie.Domain,
            cookie.Expires, cookie.Secure, cookie.HttpOnly, cookie.SameSite.ToString()));

    foreach (var frame in page.Frames)
    {
        if (!Uri.TryCreate(frame.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
        foreach (var storage in new[] { "localStorage", "sessionStorage" })
        {
            var names = await frame.EvaluateAsync<string[]>($"() => Object.keys({storage})");
            foreach (var name in names)
                observations.Add(new(scenario, path, storage, name, uri.Host, null, null, null, null));
        }
    }
}

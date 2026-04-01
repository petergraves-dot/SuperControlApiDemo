using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using petergraves.Features.SuperControlDemo;
using petergraves.Integrations.SuperControl;

namespace petergraves.Controllers;

[Route("supercontrol-demo")]
public sealed class SuperControlDemoController : Controller
{
    private readonly ISuperControlClient _superControlClient;
    private readonly ISuperControlResponseCache _responseCache;
    private readonly IOptions<SuperControlOptions> _options;

    public SuperControlDemoController(
        ISuperControlClient superControlClient,
        ISuperControlResponseCache responseCache,
        IOptions<SuperControlOptions> options)
    {
        _superControlClient = superControlClient;
        _responseCache = responseCache;
        _options = options;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View(CreateModel());
    }

    [HttpPost("")]
    public async Task<IActionResult> RunDemo(CancellationToken cancellationToken)
    {
        var model = CreateModel();
        await model.LoadDemoAsync(cancellationToken);
        return View("Index", model);
    }

    [HttpPost("refresh-cache")]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public Task<IActionResult> RefreshCache(
        [FromForm] SuperControlDemoRefreshCacheRequest request,
        CancellationToken cancellationToken)
    {
        return RefreshCacheCoreAsync(request.CacheRefreshCadence, cancellationToken);
    }

    [HttpPost("refresh-cache")]
    [Consumes("application/json")]
    [IgnoreAntiforgeryToken]
    [ServiceFilter(typeof(JsonBodyAntiforgeryFilter))]
    public Task<IActionResult> RefreshCacheJson(
        [FromBody] SuperControlDemoRefreshCacheRequest request,
        CancellationToken cancellationToken)
    {
        return RefreshCacheCoreAsync(request.CacheRefreshCadence, cancellationToken);
    }

    private async Task<IActionResult> RefreshCacheCoreAsync(
        string? cacheRefreshCadence,
        CancellationToken cancellationToken)
    {
        var model = CreateModel();
        if (!string.IsNullOrWhiteSpace(cacheRefreshCadence))
        {
            model.CacheRefreshCadence = cacheRefreshCadence;
        }

        await model.RefreshCacheAsync(cancellationToken);
        return View("Index", model);
    }

    private SuperControlDemoViewModel CreateModel()
    {
        return new SuperControlDemoViewModel(_superControlClient, _responseCache, _options);
    }
}
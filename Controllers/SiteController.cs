using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using petergraves.ViewModels;

namespace petergraves.Controllers;

public sealed class SiteController : Controller
{
    private const string FieldNotesBaseUrl = "https://petergraves.co.uk/field-notes";
    private readonly IWebHostEnvironment _environment;

    public SiteController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("/supercontrol-listing-site-tutorial")]
    public IActionResult SuperControlListingSiteTutorial()
    {
        return TutorialOrRedirect(
            "~/Views/SuperControlListingSiteTutorial/Index.cshtml",
            $"{FieldNotesBaseUrl}/supercontrol-listing-site-demo");
    }

    [HttpGet("/supercontrol-property-explorer-tutorial")]
    public IActionResult SuperControlPropertyExplorerTutorial()
    {
        return TutorialOrRedirect(
            "~/Views/PropertyExplorerTutorial/Index.cshtml",
            $"{FieldNotesBaseUrl}/map-based-property-explorer-with-supercontrol-and-leaflet");
    }

    [HttpGet("/supercontrol-listing-site-tests")]
    public IActionResult SuperControlListingSiteTests()
    {
        return View("~/Views/SuperControlListingSiteTests/Index.cshtml");
    }

    [HttpGet("/supercontrol-data-export-tutorial")]
    public IActionResult SuperControlDataExportTutorial()
    {
        return TutorialOrRedirect(
            "~/Views/SuperControlDataExportTutorial/Index.cshtml",
            $"{FieldNotesBaseUrl}/supercontrol-data-export-api");
    }

    [HttpGet("/supercontrol-data-export-tests")]
    public IActionResult SuperControlDataExportTests()
    {
        return View("~/Views/SuperControlDataExportTests/Index.cshtml");
    }

    [HttpGet("/cookies")]
    public IActionResult Cookies() => View(CookiePolicy.Current);

    [HttpGet("/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorPageViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    private IActionResult TutorialOrRedirect(string developmentViewPath, string productionUrl)
    {
        return _environment.IsDevelopment()
            ? View(developmentViewPath)
            : RedirectPermanentPreserveMethod(productionUrl);
    }
}

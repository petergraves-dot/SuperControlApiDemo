using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using petergraves.ViewModels;

namespace petergraves.Controllers;

public sealed class SiteController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("/supercontrol-listing-site-tutorial")]
    public IActionResult SuperControlListingSiteTutorial()
    {
        return View("~/Views/SuperControlListingSiteTutorial/Index.cshtml");
    }

    [HttpGet("/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorPageViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
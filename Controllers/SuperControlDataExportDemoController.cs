using Microsoft.AspNetCore.Mvc;
using petergraves.Features.SuperControlDataExportDemo;
using petergraves.ViewModels.SuperControlDataExportDemo;

namespace petergraves.Controllers;

[Route("supercontrol-data-export")]
public sealed class SuperControlDataExportDemoController : Controller
{
    private readonly IDataExportDemoViewModelFactory _viewModelFactory;

    public SuperControlDataExportDemoController(IDataExportDemoViewModelFactory viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] DataExportBookingsRequestViewModel request,
        CancellationToken cancellationToken)
    {
        var model = await _viewModelFactory.BuildBookingsAsync(request, cancellationToken);
        return View(model);
    }

    [HttpGet("properties")]
    public async Task<IActionResult> Properties(CancellationToken cancellationToken)
    {
        var model = await _viewModelFactory.BuildPropertiesAsync(cancellationToken);
        return View(model);
    }
}

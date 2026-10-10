using Microsoft.AspNetCore.Mvc;
using petergraves.Features.PropertyExplorer;
using petergraves.ViewModels.PropertyExplorer;

namespace petergraves.Controllers;

[Route("supercontrol-property-explorer")]
public sealed class PropertyExplorerController : Controller
{
    private readonly IPropertyExplorerViewModelFactory _viewModelFactory;

    public PropertyExplorerController(IPropertyExplorerViewModelFactory viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] PropertyExplorerRequestViewModel request,
        CancellationToken cancellationToken)
    {
        var model = await _viewModelFactory.BuildAsync(request, cancellationToken);
        return View(model);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] PropertyExplorerRequestViewModel request,
        CancellationToken cancellationToken)
    {
        var model = await _viewModelFactory.BuildAsync(request, cancellationToken);
        return Json(model);
    }
}

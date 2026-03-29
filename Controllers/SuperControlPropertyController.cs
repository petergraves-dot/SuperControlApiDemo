using Microsoft.AspNetCore.Mvc;
using petergraves.Features.SuperControlProperty;
using petergraves.ViewModels.SuperControlProperty;

namespace petergraves.Controllers;

[Route("supercontrol-listing-site-demo/property/{propertyId:int}")]
public sealed class SuperControlPropertyController : Controller
{
    private readonly ISuperControlPropertyViewModelFactory _viewModelFactory;

    public SuperControlPropertyController(ISuperControlPropertyViewModelFactory viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromRoute] int propertyId,
        [FromQuery] SuperControlPropertyRequestViewModel query,
        CancellationToken cancellationToken)
    {
        var request = query with { PropertyId = propertyId };
        var model = await _viewModelFactory.BuildAsync(request, cancellationToken);
        return View(model);
    }
}

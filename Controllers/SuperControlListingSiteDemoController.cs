using Microsoft.AspNetCore.Mvc;
using petergraves.Features.SuperControlListingSiteDemo;
using petergraves.ViewModels.SuperControlListingSiteDemo;

namespace petergraves.Controllers;

[Route("supercontrol-listing-site-demo")]
public sealed class SuperControlListingSiteDemoController : Controller
{
    private readonly ISuperControlListingSiteDemoViewModelFactory _viewModelFactory;

    public SuperControlListingSiteDemoController(ISuperControlListingSiteDemoViewModelFactory viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] SuperControlListingSiteDemoRequestViewModel request,
        CancellationToken cancellationToken)
    {
        var model = await _viewModelFactory.BuildAsync(request, cancellationToken);
        return View(model);
    }
}

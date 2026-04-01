using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace petergraves.Features.SuperControlDemo;

public sealed class JsonBodyAntiforgeryFilter : IAsyncAuthorizationFilter
{
    private readonly IAntiforgery _antiforgery;
    private readonly string _headerName;

    public JsonBodyAntiforgeryFilter(
        IAntiforgery antiforgery,
        IOptions<AntiforgeryOptions> antiforgeryOptions)
    {
        _antiforgery = antiforgery;
        _headerName = antiforgeryOptions.Value.HeaderName ?? "RequestVerificationToken";
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!HasRequestBody(request) || !IsJsonRequest(request))
        {
            return;
        }

        if (!request.Headers.ContainsKey(_headerName))
        {
            var requestToken = await TryExtractRequestTokenAsync(request, context.HttpContext.RequestAborted);
            if (!string.IsNullOrWhiteSpace(requestToken))
            {
                request.Headers[_headerName] = requestToken;
            }
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestResult();
        }
    }

    private static bool HasRequestBody(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        if (request.ContentLength is > 0)
        {
            return true;
        }

        return request.Headers.ContainsKey("Transfer-Encoding");
    }

    private static bool IsJsonRequest(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        return request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task<string?> TryExtractRequestTokenAsync(
        Microsoft.AspNetCore.Http.HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return document.RootElement.TryGetProperty("__RequestVerificationToken", out var requestToken)
                && requestToken.ValueKind == JsonValueKind.String
                ? requestToken.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
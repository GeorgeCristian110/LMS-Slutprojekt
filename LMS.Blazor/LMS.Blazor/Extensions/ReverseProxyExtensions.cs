using LMS.Blazor.Client.Services.ApiProxy;
using LMS.Blazor.Services.ApiProxy;
using LMS.Blazor.Services.Authentication.Tokens;
using LMS.Blazor.Services.Authentication.Tokens.Helpers;
using Microsoft.AspNetCore.Antiforgery;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

namespace LMS.Blazor.Extensions;

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddRemoteApiProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Client components are created once on the server during prerendering.
        // The browser implementation performs the real same-origin proxy request.
        services.AddScoped<IApiProxyClient, ServerNoOpApiProxyClient>();

        // Browser requests authenticate to the BFF with a cookie. Validate an
        // antiforgery token for every unsafe method before YARP adds the JWT and
        // forwards the request. Explicit validation also covers DELETE.
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(transformBuilderContext =>
            {
                transformBuilderContext.AddRequestTransform(async transformContext =>
                {
                    var httpContext = transformContext.HttpContext;

                    // Login and refresh hand out a JWT. In the BFF model only the server holds one (the browser
                    // has a cookie), so these two API paths are never reachable through the proxy.
                    // RemoteAuthenticationService calls api/auth/* directly on the API, not through here.
                    if (BlockedPrefixes.Any(prefix => httpContext.Request.Path.StartsWithSegments(prefix)))
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    if (RequiresAntiforgery(httpContext.Request.Method) &&
                        !await HasValidAntiforgeryTokenAsync(httpContext))
                    {
                        httpContext.Response.StatusCode =
                            StatusCodes.Status400BadRequest;
                        return;
                    }

                    var tokenRefreshService = httpContext.RequestServices
                        .GetRequiredService<TokenRefreshService>();

                    var tokenResult = await tokenRefreshService.GetValidAccessTokenAsync(
                        httpContext.User,
                        httpContext.RequestAborted);

                    if (tokenResult.Status == AccessTokenStatus.Unauthorized)
                    {
                        httpContext.Response.StatusCode =
                            StatusCodes.Status401Unauthorized;
                        return;
                    }

                    if (tokenResult.Status == AccessTokenStatus.Unavailable)
                    {
                        httpContext.Response.StatusCode =
                            StatusCodes.Status503ServiceUnavailable;
                        return;
                    }

                    transformContext.ProxyRequest.Headers.Authorization =
                        new AuthenticationHeaderValue(
                            "Bearer",
                            tokenResult.AccessToken);
                });
            });

        return services;
    }

    public static IEndpointConventionBuilder MapRemoteApiProxy(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapReverseProxy()
            .RequireAuthorization();

    private static async Task<bool> HasValidAntiforgeryTokenAsync(
        HttpContext httpContext)
    {
        var antiforgery = httpContext.RequestServices
            .GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static readonly string[] BlockedPrefixes = ["/api/proxy/auth", "/api/proxy/token"];

    private static bool RequiresAntiforgery(string method) =>
        !HttpMethods.IsGet(method) &&
        !HttpMethods.IsHead(method) &&
        !HttpMethods.IsOptions(method) &&
        !HttpMethods.IsTrace(method);
}

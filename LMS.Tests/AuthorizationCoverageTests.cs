using System.Reflection;
using LMS.Presentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace LMS.Tests;

/// <summary>
/// US16 guard: every API action must say which roles may call it. A forgotten [Authorize(Roles = ...)]
/// still falls back to "any logged-in user" (the Default policy in LMS.API), so a student could call a
/// teacher endpoint. This test fails before that reaches Dev.
/// </summary>
public class AuthorizationCoverageTests
{
    // Open on purpose. Adding a name here is a conscious decision, visible in the PR.
    private static readonly HashSet<string> AllowedWithoutRoles =
    [
        "AuthController.Authenticate",          // login: there is no user yet
        "TokenController.RefreshToken",         // refresh: the refresh token is the credential
        "AuthController.GetAuthenticatedUser",  // any logged-in user may read their own claims
        "DemoAuthController.Get",               // template demo page, any logged-in user
    ];

    [Fact]
    public void Every_api_action_declares_its_roles()
    {
        var unprotected = ApiActions()
            .Where(a => !AllowedWithoutRoles.Contains(a.Name) && !DeclaresRoles(a.Method))
            .Select(a => a.Name)
            .ToList();

        Assert.True(unprotected.Count == 0,
            "These actions have no [Authorize(Roles = ...)] on the method or its controller:\n  " +
            string.Join("\n  ", unprotected));
    }

    [Fact]
    public void Only_the_listed_actions_allow_anonymous()
    {
        var anonymous = ApiActions()
            .Where(a => HasAttribute<AllowAnonymousAttribute>(a.Method))
            .Select(a => a.Name)
            .Except(AllowedWithoutRoles)
            .ToList();

        Assert.True(anonymous.Count == 0,
            "These actions are [AllowAnonymous] but not in the allow list:\n  " + string.Join("\n  ", anonymous));
    }

    /* Helpers */

    // Every public method with [HttpGet]/[HttpPost]/... on a controller in LMS.Presentation
    private static IEnumerable<(string Name, MethodInfo Method)> ApiActions() =>
        typeof(AssemblyReference).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(m => ($"{m.DeclaringType!.Name}.{m.Name}", m));

    // True when the method or its controller has [Authorize] with a non-empty Roles list
    private static bool DeclaresRoles(MethodInfo method) =>
        method.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(method.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>())
            .Any(a => !string.IsNullOrWhiteSpace(a.Roles));

    private static bool HasAttribute<T>(MethodInfo method) where T : Attribute =>
        method.GetCustomAttribute<T>() is not null || method.DeclaringType!.GetCustomAttribute<T>() is not null;
}

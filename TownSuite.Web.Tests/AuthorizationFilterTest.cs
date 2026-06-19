using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;
using TownSuite.Web.SSV3Adapter;

namespace TownSuite.Web.Tests;

[TestFixture]
public class AuthorizationFilterTest
{
    private static ServiceStackAdapter BuildAdapter()
    {
        var options = new ServiceStackV3AdapterOptions(new[] { typeof(AuthFilterTestBase) })
        {
            RoutePath = "/authtest/{name}",
            SearchAssemblies = new[] { typeof(AuthorizationFilterTest).Assembly },
            SerializerSettings = new JsonSerializerSettings()
        };

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(
            new FakeHttpContextAccessor { HttpContext = new DefaultHttpContext() });
        var serviceProvider = services.BuildServiceProvider();

        return new ServiceStackAdapter(options, serviceProvider);
    }

    [Test]
    public async Task DenyingAuthorizationFilter_ShortCircuits_WithFilterStatusCode()
    {
        var adapter = BuildAdapter();

        var result = await adapter.Post("/authtest/AuthDenyRequest", "{}", "POST");

        // The filter sets UnauthorizedResult (401); the adapter must honor it and never invoke the service.
        Assert.That(result.statusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task NonDenyingAuthorizationFilter_AllowsRequest_ToReachService()
    {
        var adapter = BuildAdapter();

        var result = await adapter.Post("/authtest/AuthAllowRequest", "{}", "POST");

        Assert.That(result.statusCode, Is.EqualTo(200));
    }
}

public class AuthFilterTestBase
{
}

[AttributeUsage(AttributeTargets.Class)]
public class DenyAuthorizationAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        context.Result = new UnauthorizedResult();
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class AllowAuthorizationAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Intentionally does not set context.Result -> request is allowed to proceed.
    }
}

public class AuthDenyRequest
{
}

public class AuthAllowRequest
{
}

public class AuthResponse
{
    public bool Reached { get; set; }
}

[DenyAuthorization]
public class AuthDenyService : AuthFilterTestBase
{
    public AuthResponse Any(AuthDenyRequest request)
    {
        return new AuthResponse { Reached = true };
    }
}

[AllowAuthorization]
public class AuthAllowService : AuthFilterTestBase
{
    public AuthResponse Any(AuthAllowRequest request)
    {
        return new AuthResponse { Reached = true };
    }
}

internal class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}

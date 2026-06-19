using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace TownSuite.Web.SSV3Adapter;

public static class ServiceStackV3AdapterRouteExtensions
{
    /// <summary>
    ///     Registers the ServiceStack V3 adapter route.
    /// </summary>
    /// <remarks>
    ///     Security: the host application is responsible for configuring a production exception
    ///     handler (e.g. app.UseExceptionHandler) and for NOT enabling the developer exception page
    ///     in production. Unhandled service exceptions are rethrown by the adapter, and whether a
    ///     stack trace reaches the client is determined entirely by the host's exception handling.
    /// </remarks>
    public static void UseServiceStackV3Adapter(
        this IApplicationBuilder applicationBuilder,
        ServiceStackV3AdapterOptions options,
        IServiceProvider serviceProvider)
    {
        // Reject insecure deserialization configuration up-front. With TypeNameHandling other than
        // None, attacker-controlled request bodies can instantiate arbitrary types via $type
        // directives (gadget/RCE risk). Request bodies are fully untrusted, so fail closed here.
        if (options.SerializerSettings != null &&
            options.SerializerSettings.TypeNameHandling != TypeNameHandling.None)
        {
            throw new InvalidOperationException(
                "ServiceStackV3AdapterOptions.SerializerSettings.TypeNameHandling must be " +
                "TypeNameHandling.None. Any other value allows insecure deserialization of " +
                "attacker-controlled request bodies.");
        }

        var builder = new RouteBuilder(applicationBuilder);

        // use middlewares to configure a route
        builder.MapMiddlewarePost(options.RoutePath, appBuilder =>
        {
            appBuilder.Run(async context =>
            {
                using var prom = options?.Prometheus?.Invoke();

                string path = context.Request.Path;

                var (tooLarge, value) = await ReadBodyAsync(context, options.MaxRequestBodyBytes);
                if (tooLarge)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }

                var method = context.Request.Method;

                var Adapter = new ServiceStackAdapter(options,
                    serviceProvider,
                    prom);
                var results = await Adapter.Post(path, value, method, context.RequestAborted);

                context.Response.StatusCode = results.statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(results.json ?? "", context.RequestAborted);
            });
        });

        applicationBuilder.UseRouter(builder.Build());
    }


    /// <summary>
    ///     Reads the request body into a string, rejecting bodies larger than <paramref name="maxBytes"/>
    ///     without buffering the whole (potentially huge) payload into memory first.
    /// </summary>
    private static async Task<(bool tooLarge, string value)> ReadBodyAsync(HttpContext context, long maxBytes)
    {
        // Fast path: trust a declared Content-Length to reject oversized requests early.
        if (context.Request.ContentLength is long declared && declared > maxBytes)
        {
            return (true, string.Empty);
        }

        using var reader = new StreamReader(context.Request.Body);
        var buffer = new char[8192];
        var sb = new StringBuilder();
        long total = 0;
        int read;
        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return (true, string.Empty);
            }

            sb.Append(buffer, 0, read);
        }

        return (false, sb.ToString());
    }


    /// <summary>
    ///     Registers a route that serves a generated OpenAPI (swagger) document describing every
    ///     discovered service.
    /// </summary>
    /// <remarks>
    ///     Security: this document enumerates the full internal service/DTO surface and is served
    ///     without authentication. Do not register it in production unless the route is protected by
    ///     an authorization policy or network restriction. See the example host, which only registers
    ///     it in the Development environment.
    /// </remarks>
    public static void UseServiceStackV3AdapterSwagger(
        this IApplicationBuilder applicationBuilder,
        ServiceStackV3AdapterOptions options, string description = "Description",
        string title = "Title", string version = "1.0.0.0",
        IServiceProvider serviceProvider = null)
    {
        var builder = new RouteBuilder(applicationBuilder);

        // use middlewares to configure a route
        builder.MapMiddlewareGet(options.SwaggerPath, appBuilder =>
        {
            appBuilder.Run(async context =>
            {
                string path = context.Request.Path;

                string value;
                using (var reader = new StreamReader(context.Request.Body))
                {
                    value = await reader.ReadToEndAsync();
                }

                var swag = new Swagger(options,
                    serviceProvider == null ? builder.ServiceProvider : serviceProvider,
                    description, title, version);
                //string host = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
                var host = $"{context.Request.Host}{context.Request.PathBase}";
                var results = await swag.Generate(host);

                context.Response.StatusCode = results.statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(results.json ?? "");
            });
        });

        applicationBuilder.UseRouter(builder.Build());
    }
}
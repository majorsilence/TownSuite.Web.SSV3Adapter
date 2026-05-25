using Newtonsoft.Json;
using NUnit.Framework;
using TownSuite.Web.Example.ServiceStackExample;
using TownSuite.Web.SSV3Adapter;

namespace TownSuite.Web.Tests;

[TestFixture]
public class ExampleServiceTest
{
    [Test]
    public async Task HappyPathTest()
    {
        var options = Settings.GetSettings();
        var serviceProvider = Settings.GetServiceProvider();

        var path = "https://localhost/Example";

        var value = "";

        var adapter = new ServiceStackAdapter(options, serviceProvider);
        var results = await adapter.Post(path, value, "any");

        Assert.That(results.json, Is.EqualTo("{\"FirstName\":\"Hello\",\"LastName\":\"World\"}"));
        Assert.That(results.statusCode == 200);
    }

    [Test]
    public async Task HappyPathTest_inner_class()
    {
        var options = Settings.GetSettings();
        var serviceProvider = Settings.GetServiceProvider();

        var path = "https://localhost/ExampleOuterClass+ExampleInnerClass";

        var value = "";

        var adapter = new ServiceStackAdapter(options, serviceProvider);
        var results = await adapter.Post(path, value, "any");

        Assert.That(results.json, Is.EqualTo("{\"FirstName\":\"Hello\",\"LastName\":\"World\"}"));
        Assert.That(results.statusCode == 200);
    }

    [Test]
    public async Task HappyPathTest_cancellation_tokens()
    {
        var options = Settings.GetSettings();
        var serviceProvider = Settings.GetServiceProvider();
        var cancelSource = new CancellationTokenSource();

        var path = "https://localhost/ExampleAsyncClass";

        var value = JsonConvert.SerializeObject(new ExampleAsyncClass()
        {
            Message = "Hello World"
        });

        var adapter = new ServiceStackAdapter(options, serviceProvider);
        var results = await adapter.Post(path, value, "any", cancelSource.Token);

        Assert.That(results.json, Is.EqualTo("{\"DidCancelWithResponse\":false}"));
        Assert.That(results.statusCode == 200);
    }

    [Test]
    public async Task Should_allow_handling_cancellation_tokens_in_controller()
    {
        var options = Settings.GetSettings();
        var serviceProvider = Settings.GetServiceProvider();
        var cancelSource = new CancellationTokenSource();

        var path = "https://localhost/ExampleAsyncClass";

        var value = JsonConvert.SerializeObject(new ExampleAsyncClass()
        {
            Message = "Hello World",
            DelayMilliseconds = 100
        });

        var adapter = new ServiceStackAdapter(options, serviceProvider);
        var task = adapter.Post(path, value, "any", cancelSource.Token);
        cancelSource.Cancel();
        var results = await task;

        Assert.That(results.json, Is.EqualTo("{\"DidCancelWithResponse\":true}"));
        Assert.That(results.statusCode == 200);
    }

    [Test]
    public async Task Should_handle_exceptions_outside_of_direct_service()
    {
        var expectedException = "My Exception";
        string? exceptionMessage = null;
        var options = Settings.GetSettings();
        options.CustomCallBack = (args) => throw new Exception(expectedException);
        var serviceProvider = Settings.GetServiceProvider();
        var path = "https://localhost/Example";
        var value = "";

        options.OtherExceptionCallback =
            ex =>
            {
                exceptionMessage = ex.Message;
                return new ValueTask<(int, string?)?>((418, null));
            };
        var adapter = new ServiceStackAdapter(options, serviceProvider);
        var results = await adapter.Post(path, value, "any");

        Assert.That(exceptionMessage, Is.EqualTo(expectedException));
        Assert.That(results.statusCode == 418);
    }
}
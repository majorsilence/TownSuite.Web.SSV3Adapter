using Dapper;
using Microsoft.Data.SqlClient;

namespace TownSuite.Web.Example.ServiceStackExample;

[ExampleAttribute]
public class ExampleDataProfilingService : BaseServiceExample
{
    private readonly IConfiguration _configuration;

    public ExampleDataProfilingService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ExampleDataProfilingResponse> Any(ExampleDataProfiling request)
    {
        var cnStr = _configuration.GetConnectionString("TestDb");

        // The connection string is supplied via environment / user-secrets (no secret is committed).
        // Skip the DB call when it is not configured so the example still runs without a database.
        if (!string.IsNullOrWhiteSpace(cnStr))
        {
            using var cn = new SqlConnection(cnStr);

            await cn.OpenAsync();
            var data = await cn.QueryAsync("SELECT test_column, test_column2 FROM test_table");
        }

        return new ExampleDataProfilingResponse
        {
            Calculated = request.Number1 + request.Number2,
            Model = new ComplexModel
            {
                Message = "Hello world"
            }
        };
    }

    public void SomeOtherExampleMethod()
    {
        Console.WriteLine("SomeOtherExampleMethod called");
    }
}
namespace TownSuite.Web.Example.ServiceStackExample
{
    [Example]
    public class ExampleAsyncClassService: BaseServiceExample
    {
        public async Task<ExampleAsyncClassResponse> Any(ExampleAsyncClass request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    throw new Exception("Please Provide a Message.");
                }
                // demonstrate that it works with async methods
                await Task.Delay(request?.DelayMilliseconds ?? 100, cancellationToken);
            } catch (TaskCanceledException)
            {
                return new ExampleAsyncClassResponse()
                {
                    DidCancelWithResponse = true
                };
            }

            return new ExampleAsyncClassResponse
            {
                DidCancelWithResponse = false
            };
        }

        public void SomeOtherExampleMethod()
        {
            Console.WriteLine("SomeOtherExampleMethod called");
        }
    }
}

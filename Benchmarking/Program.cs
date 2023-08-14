using NBomber.CSharp;
using NBomber.Http;
using HttpVersion = NBomber.Http.HttpVersion;
using System.Text;
using NBomber.Contracts;
using NBomber.Http.CSharp;

var httpClientHandler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    // UseProxy = false,
    // MaxConnectionsPerServer = 1,
};

var httpClient = new HttpClient(httpClientHandler)
{
    BaseAddress = new Uri("https://localhost:6002/api/payment/health", UriKind.RelativeOrAbsolute),
    Timeout = TimeSpan.FromMinutes(5),
    DefaultRequestVersion =  new Version(2, 0),
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
    // DefaultRequestHeaders =
    // {
    //     // Connection = { "keep-alive" },
    //     // ConnectionClose = true,
    // }
};


var scenario1 = Scenario.Create("server_sent_scenario", async context =>
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(15)); // adjust as needed

        var clientArgs = new HttpClientArgs(HttpCompletionOption.ResponseHeadersRead, cts.Token);
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            Version = new Version(2, 0),
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            Headers = { { "Accept", "text/event-stream" } }
        };

        Response<HttpResponseMessage>? response = null;
        try
        {
            response = await Http.Send(httpClient, clientArgs, request);
            await using var stream = await response!.Payload.Value.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            char[] buffer = new char[1024];
            while (!cts.IsCancellationRequested && !reader.EndOfStream)
            {
                await reader.ReadAsync(buffer, 0, buffer.Length);
            }

            await stream.DisposeAsync();
        }
        catch (Exception e)
        {
            if (!cts.IsCancellationRequested)
            {
                Console.WriteLine("cts.IsCancellationRequested: " + cts.IsCancellationRequested);
                Console.WriteLine(e);
                throw;
            }
        }
        finally
        {
            response?.Payload.Value.Content.Dispose();
            response?.Payload.Value.Dispose();
        }
        return response;
    })
    .WithoutWarmUp()
    .WithLoadSimulations(
        Simulation.RampingConstant(500, TimeSpan.FromSeconds(30)),
        Simulation.RampingConstant(500, TimeSpan.FromSeconds(15)),
        Simulation.RampingConstant(0, TimeSpan.FromSeconds(10)));

NBomberRunner
    .RegisterScenarios(scenario1)
    .WithWorkerPlugins(new HttpMetricsPlugin(new[] { HttpVersion.Version1, HttpVersion.Version2, HttpVersion.Version3 }))
    .Run();


// var scenarioSimple = Scenario.Create("api_scenario", async context =>
//     {
//         var test1 = Http.CreateRequest("GET", "http://localhost:6002/api/payment?id=PaymentEvents%2F97-A");
//         return await Http.Send(httpClient, test1);
//     })
//     .WithoutWarmUp()
//     .WithLoadSimulations(
//         Simulation.RampingConstant(200, TimeSpan.FromSeconds(10)),
//         Simulation.RampingConstant(200, TimeSpan.FromSeconds(20))
//     );
// NBomberRunner
//     .RegisterScenarios(scenarioSimple)
//     .WithWorkerPlugins(new HttpMetricsPlugin(new[] { HttpVersion.Version1 }))
//     .Run();

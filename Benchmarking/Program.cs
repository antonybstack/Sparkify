
using System.Collections.Concurrent;
using NBomber.CSharp;
using NBomber.Http;
using HttpVersion = NBomber.Http.HttpVersion;
using System.Diagnostics;

    ConcurrentBag<HttpClient> httpClientPool = new ConcurrentBag<HttpClient>();


        var scenario = Scenario.Create("server_sent_scenario", async context =>
        {
            if (!httpClientPool.TryTake(out var httpClient))
                return Response.Ok(statusCode: "200", sizeBytes: 9999);

             int totalBytesRead = 0;
             var stopwatch = Stopwatch.StartNew();

             using var streamReader = new StreamReader(await httpClient.GetStreamAsync("http://localhost:6002/api/payment/health"));

             while (!streamReader.EndOfStream && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
             {
                 var message = await streamReader.ReadLineAsync();
                 totalBytesRead += message?.Length ?? 0;
             }
             Debug.WriteLine($"Total bytes read: {totalBytesRead}");
             return Response.Ok(statusCode: "200", sizeBytes: totalBytesRead);
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
             Simulation.KeepConstant(50, TimeSpan.FromSeconds(15))
        )
        .WithInit(_ =>
        {
            for (int i = 0; i < 50; i++)
                httpClientPool.Add(new HttpClient());
            return Task.CompletedTask;
        })
        .WithClean(context =>
        {
            while (!httpClientPool.IsEmpty)
            {
                if (httpClientPool.TryTake(out var httpClient))
                    httpClient.Dispose();
            }
            return Task.CompletedTask;
        });

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithWorkerPlugins(new HttpMetricsPlugin(new[] { HttpVersion.Version1 }))
            .Run();




// var scenario = Scenario.Create("server_sent_scenario", async context =>
// {
//     using var httpClient = new HttpClient();
//     int totalBytesRead = 0;
//     var buffer = new byte[1024];
//     var stopwatch = Stopwatch.StartNew();
//
//     using var streamReader = new StreamReader(await httpClient.GetStreamAsync("http://localhost:6002/api/payment/health"));
//
//     while (!streamReader.EndOfStream && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
//     {
//         var message = await streamReader.ReadLineAsync();
//         totalBytesRead += message.Length;
//     }
//     Debug.WriteLine($"Total bytes read: {totalBytesRead}");
//     return Response.Ok(statusCode: "200", sizeBytes: totalBytesRead);
// })
// .WithoutWarmUp()
// .WithLoadSimulations(
//     Simulation.KeepConstant(50, TimeSpan.FromSeconds(15))
// );
//
// NBomberRunner
// .RegisterScenarios(scenario)
// .WithWorkerPlugins(new HttpMetricsPlugin(new[] { HttpVersion.Version1 }))
// .Run();


//
// var scenario = Scenario.Create("server_sent_scenario", async context =>
//     {
//         using var httpClient = new HttpClient();
//         using var response = await httpClient.GetAsync("http://localhost:6002/api/payment/sse", HttpCompletionOption.ResponseHeadersRead);
//
//         if (response.IsSuccessStatusCode)
//         {
//             var stream = await response.Content.ReadAsStreamAsync();
//
//             // Assuming you'd like to read some bytes to count size. Adjust as per your needs.
//             var buffer = new byte[1024];
//             var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
//             int sizeBytes = bytesRead;
//
//             // Normally, for SSE, you'd continue reading in a loop, but for this test, one read might suffice.
//             return Response.Ok(statusCode: "200", sizeBytes: sizeBytes);
//         }
//         else
//         {
//             return Response.Fail(statusCode: response.StatusCode.ToString(), sizeBytes: 0);
//         }
//
//     })
//     .WithoutWarmUp()
//     .WithLoadSimulations(
//         Simulation.KeepConstant(100, TimeSpan.FromMinutes(1))  // keep 100 users for 1 minute
//     );
//
// NBomberRunner
//     .RegisterScenarios(scenario)
//     .WithWorkerPlugins(new HttpMetricsPlugin(new[] { HttpVersion.Version1 }))
//     .Run();

//
// var scenario = Scenario.Create("server_sent_scenario", async context =>
//     {
//         // consume from server sent event (SSE) endpoint
//         using var httpClient = new HttpClient();
//         var request = Http.CreateRequest("GET", "http://localhost:6002/api/payment/sse")
//             .WithHeader("Accept", "text/plain");
//         var response = await Http.Send( httpClient, request);
//         int sizeBytes = 0;
//         // using (var streamReader = new StreamReader(await httpClient.GetStreamAsync("http://localhost:6002/api/payment/sse")))
//         // {
//         //     while (!streamReader.EndOfStream)
//         //     {
//         //         var message = await streamReader.ReadLineAsync();
//         //         sizeBytes += message.Length;
//         //     }
//         // }
//
//         // return response;
//         return Response.Ok(statusCode: "200", sizeBytes: sizeBytes);
//     })
//     .WithoutWarmUp()
//     .WithLoadSimulations(Simulation.Inject(10, TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(10)));
//
// NBomberRunner
//     .RegisterScenarios(scenario)
//     .WithWorkerPlugins(new HttpMetricsPlugin(new [] {HttpVersion.Version1 }))
//     .Run();

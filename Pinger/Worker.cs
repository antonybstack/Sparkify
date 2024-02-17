namespace Pinger;

public sealed class Worker(ILogger<Worker> logger, IHttpClientFactory factory)
    : IHostedLifecycleService
{
    private HttpClient _httpClientSite;
    private HttpClient _httpClientApi;

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker starting at: {Time}", DateTimeOffset.UtcNow);

        _httpClientSite = factory.CreateClient("Ping");
        _httpClientSite.DefaultRequestHeaders.UserAgent
            .ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.33 (KHTML, like Gecko) Chrome");
        _httpClientSite.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true, MustRevalidate = true };
        _httpClientSite.BaseAddress = new Uri("https://blogs.sparkify.dev/");

        _httpClientApi = factory.CreateClient("Ping");
        _httpClientApi.DefaultRequestHeaders.UserAgent
            .ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.33 (KHTML, like Gecko) Chrome");
        _httpClientApi.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true, MustRevalidate = true };
        _httpClientApi.BaseAddress = new Uri("https://sparkify.dev/api/blog/search?query=");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <summary>
    /// Indefinite loop, pinging the _httpClientSite.BaseAddress every 5 minutes.
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClientSite.GetAsync(string.Empty, cancellationToken);
                response.EnsureSuccessStatusCode();
                logger.LogInformation("Pinged blog feed webpage at: {Time}",
                    DateTimeOffset.UtcNow);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error pinging blog feed");
            }

            try
            {
                var response = await _httpClientApi.GetAsync(string.Empty, cancellationToken);
                response.EnsureSuccessStatusCode();
                logger.LogInformation("Pinged blog feed API at: {Time}",
                    DateTimeOffset.UtcNow);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error pinging blog API");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker started at: {Time}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker is stopping at: {Time}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _httpClientSite.Dispose();
        _httpClientApi.Dispose();
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {Time}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}

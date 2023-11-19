namespace Pinger;

public sealed class Worker(ILogger<Worker> logger)
    : IHostedLifecycleService
{
    private HttpClient _httpClient;

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker starting at: {Time}", DateTimeOffset.UtcNow);

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent
            .ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.33 (KHTML, like Gecko) Chrome");
        _httpClient.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            NoCache = true, NoStore = true, MustRevalidate = true
        };
        _httpClient.BaseAddress = new Uri("https://blogs.sparkify.dev/");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <summary>
    /// Indefinite loop, pinging the _httpClient.BaseAddress every 5 minutes.
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetAsync(string.Empty, cancellationToken);
                response.EnsureSuccessStatusCode();
                logger.LogInformation("Pinged blog feed at: {Time}",
                    DateTimeOffset.UtcNow);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error pinging blog feed");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
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
        _httpClient.Dispose();
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {Time}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}

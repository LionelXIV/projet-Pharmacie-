namespace Pharmacie.Services;

public class KeepAliveService : BackgroundService
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<KeepAliveService> _logger;
    private readonly IConfiguration _config;

    public KeepAliveService(
        IHttpClientFactory clientFactory,
        ILogger<KeepAliveService> logger,
        IConfiguration config)
    {
        _clientFactory = clientFactory;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var url = _config["ApplicationUrl"]
                        ?? "https://pharmacie-saintjeanpaul-c3gscbg7eke9gfdu.francecentral-01.azurewebsites.net/health";

                    var client = _clientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(30);

                    var response = await client.GetAsync(url, stoppingToken);
                    _logger.LogInformation("KeepAlive ping : {Status}", response.StatusCode);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("KeepAlive ping failed: {Msg}", ex.Message);
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Arrêt propre du service.
        }
    }
}

namespace Company.DispatchWorker.Workers;

/// <summary>
/// Background worker that processes order-related messages.
/// </summary>
public sealed class OrderProcessingWorker : BackgroundService
{
    private readonly ILogger<OrderProcessingWorker> _logger;

    public OrderProcessingWorker(ILogger<OrderProcessingWorker> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order processing worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Worker running at: {Time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Order processing worker stopping");
    }
}

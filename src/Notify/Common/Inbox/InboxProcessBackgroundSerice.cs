namespace Notify.Common.Inbox;

public class InboxProcessBackgroundService(IServiceProvider serviceProvider,
    ILogger<InboxProcessBackgroundService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<InboxProcessBackgroundService> _logger = logger;

    // Background service responsible for polling and processing pending Inbox messages
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var assembly = typeof(IAssemblyMarker).Assembly;

        _logger.LogInformation("InboxProcessBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a dedicated scope per polling iteration to ensure scoped dependencies
                // (like DbContext and change trackers) are disposed and do not leak memory
                using var scope = _serviceProvider.CreateScope();
                var inboxService = scope.ServiceProvider.GetRequiredService<InboxService>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // Retrieve unprocessed messages from the inbox table
                var messages = await inboxService.GetUnProcessedMessagesAsync(stoppingToken);

                foreach (var message in messages)
                {
                    var type = assembly.GetType(message.MessageType);

                    if (type is null)
                    {
                        _logger.LogError("Failed to find type '{MessageType}' in assembly. Skipping message processing.", message.MessageType);
                        continue;
                    }

                    // Deserialize message payload back to its strongly-typed notification model
                    var msg = JsonSerializer.Deserialize(message.Content, type);
                    if (msg is INotification request)
                    {
                        // Publish through MediatR to invoke corresponding notification handlers
                        await mediator.Publish(request, stoppingToken);
                    }

                    // Mark message as processed to prevent duplicate executions
                    await inboxService.ProcessMessagesAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown requested; exit loop cleanly
                break;
            }
            catch (Exception ex)
            {
                // Log and swallow exception in the loop to prevent crashing the background service
                _logger.LogError(ex, "An error occurred while processing inbox messages.");
            }

            // Delay before next polling cycle with cancellation token support
            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("InboxProcessBackgroundService stopped.");
    }
}

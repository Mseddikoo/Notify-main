namespace Notify.Features.Sms;

// Background service to periodically poll SMS providers for delivery status inquiries
public class InquirySmsBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<InquirySmsBackgroundService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<InquirySmsBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InquirySmsBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a new scope per polling iteration to properly manage DbContext lifetime
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SmsDbContext>();
                var smsService = scope.ServiceProvider.GetRequiredService<SmsService>();

                // Query SMS traces that require delivery status checks from the provider
                var messages = await dbContext.SmsTraces
                    .Where(x => x.Status == SmsTraceStatus.RequiredInquiry)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    // Query provider for updated delivery status
                    message.Status = await smsService.InquiryAsync(message, stoppingToken);
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful cancellation requested on shutdown
                break;
            }
            catch (Exception ex)
            {
                // Prevent unhandled exception from terminating the background worker
                _logger.LogError(ex, "An error occurred while inquiring SMS statuses.");
            }

            // Polling interval with cancellation token support
            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("InquirySmsBackgroundService stopped.");
    }
}
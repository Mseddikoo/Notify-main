namespace Notify.Features.Email.REST.TrackingCallback;

public class TrackingCallbackEndpoint : ICarterModule
{
    // 1x1 transparent GIF byte array (Base64 decoded)
    // Used by email clients as an invisible tracking pixel to record email opens
    private static readonly byte[] TransparentGifPixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /email/tracking/{track_id}
        // Serves an invisible 1x1 GIF to email clients and records when an email is opened
        app.MapGet("/email/tracking/{track_id:required}", async (
            [FromRoute(Name = "track_id")] string trackId, 
            HttpContext httpContext,
            ILogger<TrackingCallbackEndpoint> logger,
            EmailDbContext dbContext, 
            CancellationToken cancellationToken) =>
        {
            // Prevent email clients and proxies from caching the tracking pixel response
            // to ensure each email open event triggers a new request
            httpContext.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            httpContext.Response.Headers.Pragma = "no-cache";
            httpContext.Response.Headers.Expires = "0";

            var emailTrace = await dbContext.EmailTraces.FirstOrDefaultAsync(x => x.TrackerId == trackId, cancellationToken);

            if (emailTrace is null)
            {
                logger.LogWarning("Email trace with TrackerId {TrackId} not found.", trackId);
                // Return the transparent pixel anyway so the email reader doesn't show a broken image icon
                return Results.File(TransparentGifPixel, "image/gif");
            }

            // Update status only if it hasn't already been marked as opened
            if (emailTrace.Status != EmailTraceStatus.Opened)
            {
                emailTrace.Status = EmailTraceStatus.Opened;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Email with TrackerId {TrackId} marked as Opened.", trackId);
            }

            // Return 1x1 transparent GIF file response
            return Results.File(TransparentGifPixel, "image/gif");
        }).WithTags("Email");
    }
}

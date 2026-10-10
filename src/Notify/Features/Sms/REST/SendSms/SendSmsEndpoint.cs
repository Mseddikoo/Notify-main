namespace Notify.Features.Sms.REST.SendSms;

public class SendSmsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // POST /sms - Dispatches an SMS notification request asynchronously
        app.MapPost("/sms", async (SendSmsRequest request, IMediator mediator) =>
        {
            // Validate basic required inputs
            if (string.IsNullOrWhiteSpace(request.Mobile))
            {
                return Results.BadRequest(new { Error = "Recipient phone number ('Mobile') is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { Error = "SMS content ('Message') is required." });
            }

            // Generate a new MessageId if not supplied by the caller
            var messageId = request.MessageId == Guid.Empty ? Guid.NewGuid() : request.MessageId;

            // Publish message internally via MediatR to trigger notification handlers
            var notify = new SendSmsMessage(messageId, request.Mobile, request.Message);
            await mediator.Publish(notify);

            // Return 202 Accepted indicating the message has been queued for dispatching
            return Results.Accepted($"/sms/{messageId}", new
            {
                MessageId = messageId,
                Status = "Accepted",
                Location = $"/sms/{messageId}"
            });
        }).WithTags("Sms");
    }
}

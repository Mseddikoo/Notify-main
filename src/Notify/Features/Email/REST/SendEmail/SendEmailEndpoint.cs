namespace Notify.Features.Email.REST.SendEmail;

public class SendEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // POST /email - Dispatches an email notification request asynchronously
        app.MapPost("/email", async (SendEmailRequest request, IMediator mediator) =>
        {
            // Validate basic required inputs
            if (string.IsNullOrWhiteSpace(request.To))
            {
                return Results.BadRequest(new { Error = "Recipient email address ('To') is required." });
            }

            // Generate a new MessageId if not supplied by the caller
            var messageId = request.MessageId == Guid.Empty ? Guid.NewGuid() : request.MessageId;

            // Publish message internally via MediatR to trigger notification handlers
            var notify = new SendEmailMessage(messageId, request.To, request.Subject ?? string.Empty, request.Body ?? string.Empty);
            await mediator.Publish(notify);

            // Return 202 Accepted indicating the message has been queued for dispatching
            return Results.Accepted($"/email/{messageId}", new
            {
                MessageId = messageId,
                Status = "Accepted",
                Location = $"/email/{messageId}"
            });
        }).WithTags("Email");
    }
}

namespace Notify.Features.Email.Services;

public class EmailService(
    EmailDbContext dbContext,
    IOptions<AppSettings> appsettingsOption)
{
    private readonly AppSettings _appsettings = appsettingsOption.Value;
    private readonly EmailConfiguration _emailConfiguration = appsettingsOption.Value.Features.Email;
    private readonly EmailDbContext _dbContext = dbContext;

    // Sends an email asynchronously using configured SMTP settings and registers an email trace
    public async Task SendEmailAsync(EmailMessageDto emailMessageDto, CancellationToken cancellationToken)
    {
        // Send email message asynchronously and obtain the unique tracking ID
        var trackId = await OnSendEmailAsync(emailMessageDto, cancellationToken);

        // Create trace entity with initial Sent status
        var emailTrace = CreateEmailTrace(emailMessageDto, trackId);

        // Persist trace entity in MongoDB
        await OnSaveEmailTraces(emailTrace, cancellationToken);
    }

    // Handles asynchronous SMTP dispatching and resource cleanup
    private async Task<string> OnSendEmailAsync(EmailMessageDto emailMessageDto, CancellationToken cancellationToken)
    {
        // Wrap SmtpClient in using statement to ensure underlying TCP connection is disposed properly
        using var smtpClient = CreateSmtpClient();

        var trackId = GenerateUniqueTrackId();

        // Wrap MailMessage in using statement to ensure memory and stream attachments are freed
        using var mailMessage = CreateMailMessage(emailMessageDto, trackId);

        // Asynchronously send the email to avoid blocking thread pool threads
        await smtpClient.SendMailAsync(mailMessage, cancellationToken);

        return trackId;
    }

    private async Task OnSaveEmailTraces(EmailTrace emailTrace, CancellationToken cancellationToken)
    {
        await _dbContext.EmailTraces.AddAsync(emailTrace, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private SmtpClient CreateSmtpClient()
        => new(_emailConfiguration.Host)
        {
            Port = _emailConfiguration.Port,
            Credentials = new NetworkCredential(_emailConfiguration.UserName, _emailConfiguration.Password),
            EnableSsl = true
        };

    private MailMessage CreateMailMessage(EmailMessageDto emailMessageDto, string trackId)
    {
        StringBuilder htmlBody = new StringBuilder();

        htmlBody.Append(emailMessageDto.Body);
        htmlBody.Append(GenerateTrackingPixel(trackId));

        var mailMessage = new MailMessage(
            _emailConfiguration.SenderEmail,
            emailMessageDto.To,
            emailMessageDto.Subject,
            htmlBody.ToString());

        mailMessage.IsBodyHtml = true;

        return mailMessage;
    }

    private EmailTrace CreateEmailTrace(EmailMessageDto emailMessageDto, string trackId)
        => EmailTrace.Create(emailMessageDto.To,
            emailMessageDto.Subject,
            emailMessageDto.Body,
            emailMessageDto.MessageId,
            trackId);

    private string GenerateTrackingPixel(string trackId)
    {
        return $"<img width='1' height='1' src='{_appsettings.BaseUrl}/email/tracking/{trackId}'>";
    }

    private string GenerateUniqueTrackId()
    {
        return Guid.NewGuid().ToString();
    }

}

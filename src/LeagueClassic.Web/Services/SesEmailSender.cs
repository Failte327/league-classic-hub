using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;

namespace LeagueClassic.Web.Services;

// Sends account emails (password reset, email confirmation) through Amazon
// SES's SMTP interface. Registered only when Email:SmtpUsername is
// configured — see Program.cs, which falls back to NullEmailSender otherwise
// so local dev doesn't need real SES credentials.
public class SesEmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public SesEmailSender(IConfiguration config) => _config = config;

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var section = _config.GetSection("Email");
        var host = section["Host"] ?? throw new InvalidOperationException("Email:Host is not configured.");
        var fromAddress = section["FromAddress"] ?? throw new InvalidOperationException("Email:FromAddress is not configured.");
        var smtpUsername = section["SmtpUsername"] ?? throw new InvalidOperationException("Email:SmtpUsername is not configured.");
        var smtpPassword = section["SmtpPassword"] ?? throw new InvalidOperationException("Email:SmtpPassword is not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(section["FromName"] ?? "League Classic Archive", fromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlMessage };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, int.Parse(section["Port"] ?? "587"), SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(smtpUsername, smtpPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}

// Local-dev fallback when no SMTP credentials are configured: logs instead
// of sending, so registration/password-reset flows still work without SES.
public class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger) => _logger = logger;

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("Email not configured — would have sent {Subject} to {Email}", subject, email);
        return Task.CompletedTask;
    }
}

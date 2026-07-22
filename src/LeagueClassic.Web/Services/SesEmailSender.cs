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
    private readonly ILogger<SesEmailSender> _logger;

    public SesEmailSender(IConfiguration config, ILogger<SesEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var section = _config.GetSection("Email");
        var host = section["Host"] ?? throw new InvalidOperationException("Email:Host is not configured.");
        var port = int.Parse(section["Port"] ?? "587");
        var fromAddress = section["FromAddress"] ?? throw new InvalidOperationException("Email:FromAddress is not configured.");
        var smtpUsername = section["SmtpUsername"] ?? throw new InvalidOperationException("Email:SmtpUsername is not configured.");
        var smtpPassword = section["SmtpPassword"] ?? throw new InvalidOperationException("Email:SmtpPassword is not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(section["FromName"] ?? "League Classic Archive", fromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlMessage };

        using var client = new SmtpClient();

        // Split into per-stage try/catch — SES SMTP failures (bad creds, a
        // sender identity that isn't verified yet, the account still being in
        // the SES sandbox, a network/firewall issue reaching the host) throw
        // at different stages and look identical to the caller ("send
        // failed") unless we log which stage actually blew up and why.
        try
        {
            _logger.LogInformation(
                "Connecting to SMTP {Host}:{Port} to send {Subject} to {Email}", host, port, subject, email);
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP connect to {Host}:{Port} failed", host, port);
            throw;
        }

        try
        {
            await client.AuthenticateAsync(smtpUsername, smtpPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP authentication failed for user {SmtpUsername} against {Host}", smtpUsername, host);
            throw;
        }

        try
        {
            await client.SendAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "SMTP send failed: {Subject} to {Email} from {FromAddress}", subject, email, fromAddress);
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true);
        }

        _logger.LogInformation("Sent {Subject} to {Email}", subject, email);
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

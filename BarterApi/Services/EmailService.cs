using System.Net;
using System.Net.Mail;

namespace BarterApi.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otp)
    {
        var html = $@"
<div style=""font-family: 'Segoe UI', sans-serif; max-width: 500px; margin: auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;"">
  <h2 style=""color: #6200EE; text-align: center;"">Barter Security</h2>
  <p>Hello,</p>
  <p>You requested a Two-Step Verification code for your Barter account.</p>
  <div style=""background: #f9f9f9; padding: 20px; border-radius: 8px; text-align: center; margin: 20px 0;"">
    <span style=""font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #333;"">{otp}</span>
  </div>
  <p style=""font-size: 13px; color: #777;"">This code expires in 5 minutes. If you did not request this, ignore this email.</p>
  <hr style=""border: none; border-top: 1px solid #eee; margin: 20px 0;"">
  <p style=""font-size: 11px; color: #999; text-align: center;"">© 2024 Barter App</p>
</div>";

        await SendEmailAsync(toEmail, "Your Barter Verification Code", html);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        var html = $@"
<div style=""font-family: 'Segoe UI', sans-serif; max-width: 500px; margin: auto; padding: 20px;"">
  <h2 style=""color: #6200EE;"">Password Reset</h2>
  <p>Click the link below to reset your Barter password:</p>
  <a href=""{resetLink}"" style=""background: #6200EE; color: white; padding: 12px 24px; border-radius: 6px; text-decoration: none;"">Reset Password</a>
  <p style=""color: #777; font-size: 13px; margin-top: 20px;"">Link expires in 1 hour.</p>
</div>";
        await SendEmailAsync(toEmail, "Barter Password Reset", html);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var smtpHost = _config["Email:SmtpHost"]!;
            var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var senderEmail = _config["Email:SenderEmail"]!;
            var senderPassword = _config["Email:SenderPassword"]!;
            var senderName = _config["Email:SenderName"] ?? "Barter App";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            // Don't rethrow — email failure shouldn't crash the request
        }
    }
}

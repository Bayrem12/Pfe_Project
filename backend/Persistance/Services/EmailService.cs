using Application.Interfaces;
using Domain.Common;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken)
        {
            var encodedToken = Uri.EscapeDataString(resetToken);
            var resetLink = $"{_settings.FrontendBaseUrl}/auth/reset-password?token={encodedToken}";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Reset your AutoTestify password";

            message.Body = new TextPart("html")
            {
                Text = $@"
                <div style='font-family:sans-serif;max-width:500px;margin:auto;padding:24px;'>
                    <h2 style='color:#6366f1;'>Password Reset Request</h2>
                    <p>We received a request to reset your password for your AutoTestify account.</p>
                    <p>Click the button below to set a new password.
                       This link expires in <strong>1 hour</strong>.</p>
                    <a href='{resetLink}'
                       style='display:inline-block;padding:12px 28px;background:#6366f1;color:#fff;
                              border-radius:8px;text-decoration:none;font-weight:bold;margin:20px 0;'>
                        Reset Password
                    </a>
                    <p style='color:#64748b;font-size:.875rem;'>
                        If you didn't request this, you can safely ignore this email.
                    </p>
                    <hr style='border:none;border-top:1px solid #e2e8f0;margin:24px 0;'/>
                    <small style='color:#94a3b8;'>AutoTestify — Precision Architect Automation</small>
                </div>"
            };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.Password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationToken)
        {
            var encodedToken = Uri.EscapeDataString(verificationToken);
            var verifyLink = $"{_settings.FrontendBaseUrl}/auth/verify-email-confirm?token={encodedToken}";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Verify your AutoTestify email address";

            message.Body = new TextPart("html")
            {
                Text = $@"
                <div style='font-family:sans-serif;max-width:500px;margin:auto;padding:24px;'>
                    <h2 style='color:#6366f1;'>Verify your email address</h2>
                    <p>Thanks for signing up for AutoTestify! Please confirm your email address
                       to activate your account.</p>
                    <p>Click the button below. This link expires in <strong>24 hours</strong>.</p>
                    <a href='{verifyLink}'
                       style='display:inline-block;padding:12px 28px;background:#6366f1;color:#fff;
                              border-radius:8px;text-decoration:none;font-weight:bold;margin:20px 0;'>
                        Verify Email
                    </a>
                    <p style='color:#64748b;font-size:.875rem;'>
                        If you didn't create an account, you can safely ignore this email.
                    </p>
                    <hr style='border:none;border-top:1px solid #e2e8f0;margin:24px 0;'/>
                    <small style='color:#94a3b8;'>AutoTestify — Precision Architect Automation</small>
                </div>"
            };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.Password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
    }
}
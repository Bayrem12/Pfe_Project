namespace Application.Interfaces
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string resetToken);
        Task SendVerificationEmailAsync(string toEmail, string verificationToken);
    }
}
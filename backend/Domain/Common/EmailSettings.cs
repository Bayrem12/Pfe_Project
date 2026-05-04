namespace Domain.Common
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = default!;
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; } = default!;
        public string SenderName { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string FrontendBaseUrl { get; set; } = default!;
    }
}
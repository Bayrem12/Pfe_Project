namespace Application.Interfaces
{
    public class OAuthUserInfo
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
    }

    public interface IGoogleAuthService
    {
        Task<OAuthUserInfo?> GetUserFromCodeAsync(string code, string redirectUri);
    }
}

namespace Application.Interfaces
{
    public interface IGithubAuthService
    {
        Task<OAuthUserInfo?> GetUserFromCodeAsync(string code, string redirectUri);
    }
}
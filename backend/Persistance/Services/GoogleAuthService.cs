using System.Net.Http.Json;
using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public GoogleAuthService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public async Task<OAuthUserInfo?> GetUserFromCodeAsync(
            string code,
            string redirectUri)
        {
            // 1. Échanger le code contre un access_token
            var tokenResponse = await _http.PostAsJsonAsync(
                "https://oauth2.googleapis.com/token",
                new
                {
                    code,
                    client_id = _config["OAuth:Google:ClientId"],
                    client_secret = _config["OAuth:Google:ClientSecret"],
                    redirect_uri = redirectUri,
                    grant_type = "authorization_code"
                }
            );

            if (!tokenResponse.IsSuccessStatusCode) return null;

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonDocument.Parse(tokenJson).RootElement;

            if (!tokenData.TryGetProperty("access_token", out var accessTokenEl))
                return null;

            var accessToken = accessTokenEl.GetString();

            // 2. Récupérer le profil utilisateur Google
            var profileRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "https://www.googleapis.com/oauth2/v2/userinfo"
            );
            profileRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", accessToken);

            var profileResponse = await _http.SendAsync(profileRequest);
            if (!profileResponse.IsSuccessStatusCode) return null;

            var profileJson = await profileResponse.Content.ReadAsStringAsync();
            var profile = JsonDocument.Parse(profileJson).RootElement;

            return new OAuthUserInfo
            {
                Email = profile.GetProperty("email").GetString() ?? "",
                FirstName = profile.TryGetProperty("given_name", out var fn)
                                ? fn.GetString() ?? "" : "",
                LastName = profile.TryGetProperty("family_name", out var ln)
                                ? ln.GetString() ?? "" : "",
                ProviderId = profile.GetProperty("id").GetString() ?? ""
            };
        }
    }
}
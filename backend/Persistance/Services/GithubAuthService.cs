using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Persistance.Services
{
    public class GithubAuthService : IGithubAuthService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public GithubAuthService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public async Task<OAuthUserInfo?> GetUserFromCodeAsync(
            string code,
            string redirectUri)
        {
            // 1. Échanger le code contre un access_token
            var tokenPayload = new Dictionary<string, string>
            {
                { "code",          code },
                { "client_id",     _config["OAuth:Github:ClientId"]!     },
                { "client_secret", _config["OAuth:Github:ClientSecret"]! },
                { "redirect_uri",  redirectUri }
            };

            var tokenRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "https://github.com/login/oauth/access_token"
            );
            tokenRequest.Content = new FormUrlEncodedContent(tokenPayload);
            tokenRequest.Headers.Add("Accept", "application/json");

            var tokenResponse = await _http.SendAsync(tokenRequest);
            if (!tokenResponse.IsSuccessStatusCode) return null;

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonDocument.Parse(tokenJson).RootElement;

            if (!tokenData.TryGetProperty("access_token", out var accessTokenEl))
                return null;

            var accessToken = accessTokenEl.GetString();

            // 2. Récupérer le profil utilisateur
            var profileRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.github.com/user"
            );
            profileRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", accessToken);
            profileRequest.Headers.Add("User-Agent", "AutoTestify");

            var profileResponse = await _http.SendAsync(profileRequest);
            if (!profileResponse.IsSuccessStatusCode) return null;

            var profileJson = await profileResponse.Content.ReadAsStringAsync();
            var profile = JsonDocument.Parse(profileJson).RootElement;

            // 3. Récupérer l'email
            var email = profile.TryGetProperty("email", out var emailEl)
                ? emailEl.GetString() ?? "" : "";

            // Si email vide → appel à /user/emails
            if (string.IsNullOrEmpty(email))
            {
                var emailRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://api.github.com/user/emails"
                );
                emailRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", accessToken);
                emailRequest.Headers.Add("User-Agent", "AutoTestify");

                var emailResponse = await _http.SendAsync(emailRequest);
                if (emailResponse.IsSuccessStatusCode)
                {
                    var emailJson = await emailResponse.Content.ReadAsStringAsync();
                    var emailArray = JsonDocument.Parse(emailJson).RootElement;

                    foreach (var e in emailArray.EnumerateArray())
                    {
                        if (e.TryGetProperty("primary", out var primary) &&
                            primary.GetBoolean() &&
                            e.TryGetProperty("email", out var emailProp))
                        {
                            email = emailProp.GetString() ?? "";
                            break;
                        }
                    }
                }
            }

            // 4. Parser le nom
            var fullName = profile.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() ?? "" : "";
            var nameParts = fullName.Split(' ', 2);
            var firstName = nameParts.Length > 0 ? nameParts[0] : "";
            var lastName = nameParts.Length > 1 ? nameParts[1] : "";

            var providerId = profile.TryGetProperty("id", out var idEl)
                ? idEl.GetInt64().ToString() : "";

            return new OAuthUserInfo
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                ProviderId = providerId
            };
        }
    }
}
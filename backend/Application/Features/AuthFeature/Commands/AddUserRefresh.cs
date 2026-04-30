using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.UserFeature.Commands
{
    /// <summary>
    /// Commande pour rafraîchir un token (POST /api/auth/refresh).
    ///
    /// ÉTAT : Non implémenté — l'endpoint est désactivé dans AuthController.
    /// Pour l'implémenter correctement il faut :
    ///   1. Stocker un refresh token hashé en base avec sa date d'expiration
    ///   2. Créer un ITokenService qui valide le refresh token et émet un nouvel access token
    ///   3. Invalider l'ancien refresh token après usage (rotation)
    ///
    /// Mentionné dans le mémoire comme limitation connue.
    /// </summary>
    public record AddUserRefresh(string RefreshToken) : IRequest<ResponseHttp>
    {
        public class AddUserRefreshHandler : IRequestHandler<AddUserRefresh, ResponseHttp>
        {
            public Task<ResponseHttp> Handle(AddUserRefresh request, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                    return Task.FromResult(new ResponseHttp
                    {
                        Fail_Messages = "Le refresh token est requis.",
                        Status = StatusCodes.Status400BadRequest
                    });

                return Task.FromResult(new ResponseHttp
                {
                    Fail_Messages = "Le refresh token n'est pas encore implémenté.",
                    Status = StatusCodes.Status501NotImplemented
                });
            }
        }
    }
}

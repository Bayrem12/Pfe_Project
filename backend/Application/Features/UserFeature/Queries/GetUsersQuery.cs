using Application.Interfaces.Identity;
using Application.Setting;
using Application.Users.DTOs;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.UserFeature.Queries
{
    public record GetUsersQuery() : IRequest<ResponseHttp>;

    public class GetAllUsersQueryHandler : IRequestHandler<GetUsersQuery, ResponseHttp>
    {
        private readonly IUserRepository _userRepo;

        public GetAllUsersQueryHandler(IUserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public async Task<ResponseHttp> Handle(GetUsersQuery query, CancellationToken cancellationToken)
        {
            var users = await _userRepo.GetAllUsersAsync();

            var result = users.Select(u => new UserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                IsActive = u.IsActive,
                Roles = u.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>(),
                CreatedDate = u.CreatedDate.HasValue ? u.CreatedDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                ModifiedDate = u.ModifiedDate.HasValue ? u.ModifiedDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null
            }).ToList();

            return new ResponseHttp
            {
                Resultat = result,
                Status = StatusCodes.Status200OK
            };
        }
    }
}

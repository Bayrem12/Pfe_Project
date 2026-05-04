using Application.Interfaces.Identity;
using Application.Users.DTOs;
using MediatR;

namespace Application.Features.UserFeature.Queries
{
    public record GetUserByIdQuery(Guid Id) : IRequest<UserDto?>;

    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
    {
        private readonly IUserRepository _userRepo;

        public GetUserByIdQueryHandler(IUserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public async Task<UserDto?> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
        {
            var user = await _userRepo.GetUserByIdAsync(query.Id);

            if (user == null) return null;

            return new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                IsActive = user.IsActive,
                Roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>(),
                CreatedDate = user.CreatedDate.HasValue ? user.CreatedDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                ModifiedDate = user.ModifiedDate.HasValue ? user.ModifiedDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null
            };
        }
    }
}

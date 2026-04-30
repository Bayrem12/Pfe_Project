using Application.Interfaces.Identity;
using Application.Users.DTOs;
using MediatR;

namespace Application.Features.UserFeature.Queries
{
    public record GetSearchUserQuery(string Keyword) : IRequest<List<UserDto>>;

    /// <summary>
    /// Handler pour exécuter la query GetSearchUserQuery
    /// </summary>
    public class GetUsersSearchQueryHandler : IRequestHandler<GetSearchUserQuery, List<UserDto>>
    {
        private readonly IUserRepository _userRepository;

        public GetUsersSearchQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>
        /// Exécute la recherche et mappe les utilisateurs vers UserDto
        /// </summary>
        public async Task<List<UserDto>> Handle(GetSearchUserQuery query, CancellationToken cancellationToken)
        {
            var users = await _userRepository.SearchUsersAsync(query.Keyword);

            return users.Select(u => new UserDto
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
        }
    }
}

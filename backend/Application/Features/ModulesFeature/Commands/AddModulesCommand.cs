using Application.Interfaces;
using Application.Interfaces.Identity;
using Application.Interfaces.Repositories;
using Domain.Entities.ProjectManagement;
using Domain.Enums;                          // ✅ pour ProjectRole
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;                           // ✅ pour .FirstOrDefault()
using System.Security.Claims;

namespace Application.Features.ModulesFeature.Commands
{
    public record AddModulesCommand(
        Guid ProjectId,
        string Name,
        string Description,
        int DisplayOrder) : IRequest<Guid>;

    public class AddModulesCommandHandler : IRequestHandler<AddModulesCommand, Guid>
    {
        private readonly IModulesRepository _modulesRepository;
        private readonly ITestTestAutoumatisationContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;

        public AddModulesCommandHandler(
            IModulesRepository modulesRepository,
            ITestTestAutoumatisationContext context,
            IHttpContextAccessor httpContextAccessor,
            IUserRepository userRepository)
        {
            _modulesRepository = modulesRepository;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
        }

        public async Task<Guid> Handle(AddModulesCommand request, CancellationToken cancellationToken)
        {
            // Vérifier que le projet existe
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

            if (project == null)
                throw new InvalidOperationException(
                    $"Le projet '{request.ProjectId}' n'existe pas.");

            // ✅ Fix FindFirstValue → .Claims.FirstOrDefault() (pas de dépendance ASP.NET Core)
            var currentUserId = _httpContextAccessor.HttpContext?.User
                .Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(currentUserId))
                throw new UnauthorizedAccessException("Utilisateur non authentifié.");

            var currentUserGuid = Guid.Parse(currentUserId);

            // ✅ Fix ProjectMembers → utilise p.Members (nom réel dans l'entité Project)
            // ✅ Fix Role == "Owner" → Role == ProjectRole.Owner (c'est un enum, pas une string)
            var isMember = await _context.Projects
                .AnyAsync(p => p.Id == request.ProjectId
                    && p.Members.Any(m =>
                        m.UserId == currentUserGuid &&
                        (m.Role == ProjectRole.Owner || m.Role == ProjectRole.Tester)),
                    cancellationToken);

            if (!isMember)
                throw new UnauthorizedAccessException(
                    "Vous devez être Owner ou Tester du projet pour créer un module.");

            var module = new Module
            {
                ProjectId = request.ProjectId,
                Name = request.Name,
                Description = request.Description,
                DisplayOrder = request.DisplayOrder
            };

            var result = await _modulesRepository.AddAsync(module);

            return result.Id;
        }
    }
}
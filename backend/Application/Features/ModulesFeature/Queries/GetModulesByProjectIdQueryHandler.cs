using Application.Features.ModulesFeature.DTOs;
using Application.Interfaces.Repositories;
using MediatR;

namespace Application.Features.Modules.Queries
{
    public class GetModulesByProjectIdQueryHandler
        : IRequestHandler<GetModulesByProjectIdQuery, List<ModulesDTO>>
    {
        private readonly IModulesRepository _modulesRepository;

        public GetModulesByProjectIdQueryHandler(IModulesRepository modulesRepository)
        {
            _modulesRepository = modulesRepository;
        }

        public async Task<List<ModulesDTO>> Handle(
            GetModulesByProjectIdQuery request,
            CancellationToken cancellationToken)
        {
            var modules = await _modulesRepository
                .GetByProjectIdAsync(request.ProjectId);

            return modules.Select(m => new ModulesDTO
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                DisplayOrder = m.DisplayOrder,
                ProjectId = m.ProjectId
            }).ToList();
        }
    }
}
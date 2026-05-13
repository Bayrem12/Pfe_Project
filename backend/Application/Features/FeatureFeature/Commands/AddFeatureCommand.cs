using Application.Features.FeatureFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Entities.ProjectManagement;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.FeatureFeature.Commands
{
    public record AddFeatureCommand(
        Guid ModuleId,
        string Name,
        string Description,
        int DisplayOrder,
        Guid CreatedById
    ) : IRequest<ResponseHttp>;

    public class AddFeatureCommandHandler : IRequestHandler<AddFeatureCommand, ResponseHttp>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly ITestTestAutoumatisationContext _context;
        private readonly IMapper _mapper;

        public AddFeatureCommandHandler(IFeatureRepository featureRepository, ITestTestAutoumatisationContext context, IMapper mapper)
        {
            _featureRepository = featureRepository;
            _context = context;
            _mapper = mapper;
        }

        public async Task<ResponseHttp> Handle(AddFeatureCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // No duplicate feature name in the same module
                var nameExists = await _context.Features
                    .AnyAsync(f => f.ModuleId == request.ModuleId
                              && f.Name.ToLower() == request.Name.ToLower()
                              && !f.IsDeleted, cancellationToken);

                if (nameExists)
                    return new ResponseHttp
                    {
                        FailMessages = $"A feature named '{request.Name}' already exists in this module.",
                        Status = StatusCodes.Status409Conflict
                    };

                // Création de l'entité Feature
                var feature = new Feature
                {
                    Id = Guid.NewGuid(),
                    ModuleId = request.ModuleId,
                    Name = request.Name,
                    Description = request.Description,
                    DisplayOrder = request.DisplayOrder,

                    CreatedDate = DateTime.UtcNow
                };

                feature = await _featureRepository.Post(feature);
                await _featureRepository.SaveChange(cancellationToken);

                return new ResponseHttp
                {
                    Resultat = _mapper.Map<FeatureDTO>(feature),
                    Status = StatusCodes.Status200OK
                };
            }
            catch (Exception ex)
            {
                return new ResponseHttp
                {
                    FailMessages = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                };
            }
        }
    }
}
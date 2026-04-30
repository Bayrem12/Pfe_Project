using Application.Features.FeatureFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Entities.ProjectManagement;
using MediatR;
using Microsoft.AspNetCore.Http;

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
        private readonly IMapper _mapper;

        public AddFeatureCommandHandler(IFeatureRepository featureRepository, IMapper mapper)
        {
            _featureRepository = featureRepository;
            _mapper = mapper;
        }

        public async Task<ResponseHttp> Handle(AddFeatureCommand request, CancellationToken cancellationToken)
        {
            try
            {
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
                    Fail_Messages = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                };
            }
        }
    }
}
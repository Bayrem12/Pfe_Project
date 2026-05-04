using Application.Features.FeatureFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.FeatureFeature.Commands
{
    public record UpdateFeatureCommand(
        Guid FeatureId,
        string Name,
        string Description,
        int DisplayOrder,
        Guid UpdatedById
    ) : IRequest<ResponseHttp>;

    public class UpdateFeatureCommandHandler : IRequestHandler<UpdateFeatureCommand, ResponseHttp>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly IMapper _mapper;

        public UpdateFeatureCommandHandler(IFeatureRepository featureRepository, IMapper mapper)
        {
            _featureRepository = featureRepository;
            _mapper = mapper;
        }

        public async Task<ResponseHttp> Handle(UpdateFeatureCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var feature = await _featureRepository.GetById(request.FeatureId);

                if (feature == null)
                {
                    return new ResponseHttp
                    {
                        FailMessages = "Feature not found",
                        Status = StatusCodes.Status404NotFound
                    };
                }

                feature.Name = request.Name;
                feature.Description = request.Description;
                feature.DisplayOrder = request.DisplayOrder;
                feature.ModifiedDate = DateTime.UtcNow;
                feature.ModifiedById = request.UpdatedById.ToString();

                feature = await _featureRepository.Update(feature);
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

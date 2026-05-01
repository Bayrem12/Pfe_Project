using Application.Features.FeatureFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.FeatureFeature.Queries
{
    public record GetFeaturesByModuleQuery(Guid ModuleId) : IRequest<ResponseHttp>;

    public class GetFeaturesByModuleQueryHandler : IRequestHandler<GetFeaturesByModuleQuery, ResponseHttp>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly IMapper _mapper;

        public GetFeaturesByModuleQueryHandler(IFeatureRepository featureRepository, IMapper mapper)
        {
            _featureRepository = featureRepository;
            _mapper = mapper;
        }

        public async Task<ResponseHttp> Handle(GetFeaturesByModuleQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var features = await _featureRepository.GetByModuleId(request.ModuleId);
                var featuresDto = _mapper.Map<List<FeatureListDTO>>(features);

                return new ResponseHttp
                {
                    Resultat = featuresDto,
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

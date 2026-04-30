using Application.Features.FeatureFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.FeatureFeature.Queries
{
    public record GetFeatureByIdQuery(Guid FeatureId) : IRequest<ResponseHttp>;

    public class GetFeatureByIdQueryHandler : IRequestHandler<GetFeatureByIdQuery, ResponseHttp>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly IMapper _mapper;

        public GetFeatureByIdQueryHandler(IFeatureRepository featureRepository, IMapper mapper)
        {
            _featureRepository = featureRepository;
            _mapper = mapper;
        }

        public async Task<ResponseHttp> Handle(GetFeatureByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var feature = await _featureRepository.GetById(request.FeatureId);

                if (feature == null)
                {
                    return new ResponseHttp
                    {
                        Fail_Messages = "Feature not found",
                        Status = StatusCodes.Status404NotFound
                    };
                }

                var featureDto = _mapper.Map<FeatureDTO>(feature);

                return new ResponseHttp
                {
                    Resultat = featureDto,
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

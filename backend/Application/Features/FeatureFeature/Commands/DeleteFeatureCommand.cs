using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.FeatureFeature.Commands
{
    public record DeleteFeatureCommand(Guid FeatureId) : IRequest<ResponseHttp>;

    public class DeleteFeatureCommandHandler : IRequestHandler<DeleteFeatureCommand, ResponseHttp>
    {
        private readonly IFeatureRepository _featureRepository;

        public DeleteFeatureCommandHandler(IFeatureRepository featureRepository)
        {
            _featureRepository = featureRepository;
        }

        public async Task<ResponseHttp> Handle(DeleteFeatureCommand request, CancellationToken cancellationToken)
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

                await _featureRepository.SoftDelete(feature);
                await _featureRepository.SaveChange(cancellationToken);

                return new ResponseHttp
                {
                    Resultat = true,
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

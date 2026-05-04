using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.NlpFeature.Commands
{
    public record DeleteActionMappingCommand(Guid MappingId) : IRequest<ResponseHttp>;

    public class DeleteActionMappingCommandHandler : IRequestHandler<DeleteActionMappingCommand, ResponseHttp>
    {
        private readonly IActionMappingRepository _repo;

        public DeleteActionMappingCommandHandler(IActionMappingRepository repo)
        {
            _repo = repo;
        }

        public async Task<ResponseHttp> Handle(DeleteActionMappingCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var mapping = await _repo.GetByIdAsync(request.MappingId, cancellationToken);
                if (mapping == null)
                {
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status404NotFound,
                        FailMessages = "Action mapping not found."
                    };
                }

                await _repo.SoftDelete(mapping.Id);
                await _repo.SaveChange(cancellationToken);

                return new ResponseHttp
                {
                    Status = StatusCodes.Status200OK,
                    Resultat = true
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

using Application.Features.NlpFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.NlpFeature.Commands
{
    public record ToggleActionMappingStatusCommand(
        Guid MappingId,
        bool IsActive
    ) : IRequest<ResponseHttp>;

    public class ToggleActionMappingStatusCommandHandler : IRequestHandler<ToggleActionMappingStatusCommand, ResponseHttp>
    {
        private readonly IActionMappingRepository _repo;

        public ToggleActionMappingStatusCommandHandler(IActionMappingRepository repo)
        {
            _repo = repo;
        }

        public async Task<ResponseHttp> Handle(ToggleActionMappingStatusCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var mapping = await _repo.GetByIdAsync(request.MappingId, cancellationToken);
                if (mapping == null)
                {
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status404NotFound,
                        Fail_Messages = "Action mapping not found."
                    };
                }

                mapping.IsActive = request.IsActive;
                await _repo.Update(mapping);
                await _repo.SaveChange(cancellationToken);

                var dto = new ActionMappingDto
                {
                    Id = mapping.Id,
                    IntentPattern = mapping.IntentPattern,
                    ActionType = mapping.ActionType.ToString(),
                    SelectorStrategy = mapping.SelectorStrategy,
                    SelectorValue = mapping.SelectorValue,
                    Description = mapping.Description ?? string.Empty,
                    IsActive = mapping.IsActive
                };

                return new ResponseHttp
                {
                    Resultat = dto,
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

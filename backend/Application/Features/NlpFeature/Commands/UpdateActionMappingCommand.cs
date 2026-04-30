using Application.Features.NlpFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.NlpFeature.Commands
{
    public record UpdateActionMappingCommand(
        Guid MappingId,
        string IntentPattern,
        string ActionType,
        string SelectorStrategy,
        string SelectorValue,
        string Description,
        int Priority = 0
    ) : IRequest<ResponseHttp>;

    public class UpdateActionMappingCommandHandler : IRequestHandler<UpdateActionMappingCommand, ResponseHttp>
    {
        private readonly IActionMappingRepository _repo;

        public UpdateActionMappingCommandHandler(IActionMappingRepository repo)
        {
            _repo = repo;
        }

        public async Task<ResponseHttp> Handle(UpdateActionMappingCommand request, CancellationToken cancellationToken)
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

                if (!Enum.TryParse<UIActionType>(request.ActionType, ignoreCase: true, out var actionType))
                {
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Fail_Messages = $"Invalid action type: '{request.ActionType}'. Valid values: {string.Join(", ", Enum.GetNames<UIActionType>())}"
                    };
                }

                mapping.IntentPattern = request.IntentPattern;
                mapping.ActionType = actionType;
                mapping.SelectorStrategy = request.SelectorStrategy;
                mapping.SelectorValue = request.SelectorValue;
                mapping.Description = request.Description;
                mapping.Priority = request.Priority;

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
                    IsActive = mapping.IsActive,
                    Priority = mapping.Priority
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

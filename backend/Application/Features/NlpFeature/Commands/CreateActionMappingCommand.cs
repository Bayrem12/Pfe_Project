// COMMAND: Create a new action mapping for a project
// Endpoint: POST /api/Nlp/action-mappings/{projectId}
// Request body: { intentPattern, actionType, selectorStrategy, selectorValue, description }
// Response: 201 Created with the full ActionMapping object
//
// An ActionMapping links an NLP intent pattern to a UI automation action.
// Example: When NLP detects "click login" → tell Playwright to click the "#loginBtn" element.

using Application.Features.NlpFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Entities.NLP;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.NlpFeature.Commands
{
    /// <summary>
    /// Command to create a new action mapping for a project.
    /// Records are immutable classes — perfect for commands that carry data.
    /// Each property maps to a field in the JSON request body.
    /// </summary>
    public record CreateActionMappingCommand(
        Guid ProjectId,              // Comes from the URL path: /action-mappings/{projectId}
        string IntentPattern,        // Regex pattern to match intents (e.g., "click.*login")
        string ActionType,           // UI action type as string (e.g., "Click") — we parse it to the enum
        string SelectorStrategy,     // How to find the element: "css", "xpath", "id"
        string SelectorValue,        // The actual selector: "#loginBtn", "//button[@id='login']"
        string Description,          // Human-readable description
        int Priority = 0             // Higher = evaluated first; default 0
    ) : IRequest<ResponseHttp>
    {
        public class CreateActionMappingCommandHandler : IRequestHandler<CreateActionMappingCommand, ResponseHttp>
        {
            private readonly IActionMappingRepository _actionMappingRepository;
            private readonly IMapper _mapper;

            public CreateActionMappingCommandHandler(
                IActionMappingRepository actionMappingRepository,
                IMapper mapper)
            {
                _actionMappingRepository = actionMappingRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(CreateActionMappingCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    // 1. Parse the ActionType string to the UIActionType enum
                    // Enum.TryParse converts "Click" → UIActionType.Click
                    // ignoreCase: true means "click", "Click", "CLICK" all work
                    if (!Enum.TryParse<UIActionType>(request.ActionType, ignoreCase: true, out var actionType))
                    {
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status400BadRequest,
                            Fail_Messages = $"Invalid action type: '{request.ActionType}'. " +
                                $"Valid values are: {string.Join(", ", Enum.GetNames<UIActionType>())}"
                        };
                    }

                    // 2. Create the ActionMapping entity
                    var actionMapping = new ActionMapping
                    {
                        ProjectId = request.ProjectId,
                        IntentPattern = request.IntentPattern,
                        ActionType = actionType,
                        SelectorStrategy = request.SelectorStrategy,
                        SelectorValue = request.SelectorValue,
                        Description = request.Description,
                        IsActive = true, // New mappings are active by default
                        Priority = request.Priority
                    };

                    // 3. Save to database
                    await _actionMappingRepository.Post(actionMapping);
                    await _actionMappingRepository.SaveChange(cancellationToken);

                    // 4. Map to DTO and return with 201 Created status
                    var dto = _mapper.Map<ActionMappingDto>(actionMapping);

                    return new ResponseHttp
                    {
                        Resultat = dto,
                        Status = StatusCodes.Status201Created // 201 = resource was created successfully
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
}

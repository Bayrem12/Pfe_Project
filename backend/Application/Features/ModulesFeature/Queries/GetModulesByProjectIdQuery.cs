using Application.Features.ModulesFeature.DTOs;
using MediatR;

public record GetModulesByProjectIdQuery(Guid ProjectId) : IRequest<List<ModulesDTO>>
{
}

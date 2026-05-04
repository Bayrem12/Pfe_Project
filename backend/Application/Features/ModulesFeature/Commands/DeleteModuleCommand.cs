using Application.Interfaces.Repositories;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ModulesFeature.Commands
{
    public record DeleteModuleCommand(Guid ModuleId) : IRequest<ResponseHttp>;

    public class DeleteModuleCommandHandler : IRequestHandler<DeleteModuleCommand, ResponseHttp>
    {
        private readonly IModulesRepository _modulesRepository;

        public DeleteModuleCommandHandler(IModulesRepository modulesRepository)
        {
            _modulesRepository = modulesRepository;
        }

        public async Task<ResponseHttp> Handle(DeleteModuleCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var module = await _modulesRepository.GetById(request.ModuleId);

                if (module == null)
                {
                    return new ResponseHttp
                    {
                        FailMessages = "Module not found",
                        Status = StatusCodes.Status404NotFound
                    };
                }

                var deleted = await _modulesRepository.SoftDelete(request.ModuleId);
                if (!deleted)
                {
                    return new ResponseHttp
                    {
                        FailMessages = "Unable to delete module",
                        Status = StatusCodes.Status400BadRequest
                    };
                }

                await _modulesRepository.SaveChange(cancellationToken);

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
                    FailMessages = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                };
            }
        }
    }
}

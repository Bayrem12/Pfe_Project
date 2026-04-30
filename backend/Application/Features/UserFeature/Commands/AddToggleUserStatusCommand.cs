using Application.Features.UserFeature.Dtos; // ton UserDTO
using Application.Interfaces.Identity;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.UserFeature.Commands
{
    public record AddToggleUserStatusCommand(Guid UserId) : IRequest<ResponseHttp>
    {
        public class AddToggleUserStatusCommandHandler : IRequestHandler<AddToggleUserStatusCommand, ResponseHttp>
        {
            private readonly IUserRepository _userRepo;
            private readonly IMapper _mapper;

            public AddToggleUserStatusCommandHandler(IUserRepository userRepo, IMapper mapper)
            {
                _userRepo = userRepo;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(AddToggleUserStatusCommand command, CancellationToken cancellationToken)
            {
                try
                {
                    // Toggle le statut
                    await _userRepo.ToggleUserStatusAsync(command.UserId);

                    // Récupère l'utilisateur mis à jour
                    var updatedUser = await _userRepo.GetUserByIdAsync(command.UserId);

                    if (updatedUser == null)
                    {
                        return new ResponseHttp()
                        {
                            Fail_Messages = "Utilisateur introuvable",
                            Status = StatusCodes.Status404NotFound
                        };
                    }

                    // Mapper vers UserDTO
                    var userDto = _mapper.Map<UserDTO>(updatedUser);

                    return new ResponseHttp()
                    {
                        Resultat = userDto,
                        Status = StatusCodes.Status200OK
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp()
                    {
                        Fail_Messages = ex.Message,
                        Status = StatusCodes.Status400BadRequest
                    };
                }
            }
        }
    }
}
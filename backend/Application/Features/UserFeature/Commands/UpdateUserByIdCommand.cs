using Application.Features.UserFeature.Dtos;
using Application.Features.UserFeature.DTOs;
using Application.Interfaces.Identity;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.UserFeature.Commands
{
    public record UpdateUserByIdCommand(
        Guid userId,
        string firstName,
        string lastName,
        string email,
        bool isActive
    ) : IRequest<ResponseHttp>
    {
        public class UpdateUserCommandHandler : IRequestHandler<UpdateUserByIdCommand, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;
            private readonly IMapper _mapper;

            public UpdateUserCommandHandler(IUserRepository userRepository, IMapper mapper)
            {
                _userRepository = userRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(UpdateUserByIdCommand request, CancellationToken cancellationToken)
            {
                var user = await _userRepository.GetUserByIdAsync(request.userId);

                if (user == null)
                {
                    return new ResponseHttp
                    {
                        Resultat = null,
                        Fail_Messages = "User with this Id not found.",
                        Status = StatusCodes.Status400BadRequest
                    };
                }

                // Mettre à jour les propriétés
                user.FirstName = request.firstName;
                user.LastName = request.lastName;
                user.Email = request.email;
                user.IsActive = request.isActive;

                await _userRepository.UpdateUserAsync(user);

                // Mapper vers DTO pour retourner
                var userDto = _mapper.Map<UserDTO>(user);

                return new ResponseHttp
                {
                    Resultat = userDto,
                    Status = StatusCodes.Status200OK
                };
            }
        }
    }
}
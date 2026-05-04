using Application.Features.ScenariosFeature.DTOs;
using Application.Setting;
using Domain.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ScenariosFeature.Commands
{
    public record ValidateGherkinCommand(string GherkinContent) : IRequest<ResponseHttp>
    {
        public class ValidateGherkinCommandHandler : IRequestHandler<ValidateGherkinCommand, ResponseHttp>
        {
            private readonly IGherkinParserService _gherkinParser;

            public ValidateGherkinCommandHandler(IGherkinParserService gherkinParser)
            {
                _gherkinParser = gherkinParser;
            }

            public Task<ResponseHttp> Handle(ValidateGherkinCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var isValid = _gherkinParser.ValidateSyntax(request.GherkinContent, out var errors);

                    var result = new ValidationResultDto
                    {
                        IsValid = isValid,
                        Errors = errors.ToList()
                    };

                    return Task.FromResult(new ResponseHttp
                    {
                        Resultat = result,
                        Status = StatusCodes.Status200OK
                    });
                }
                catch (Exception ex)
                {
                    var innerMessage = ex.InnerException?.Message ?? ex.Message;
                    return Task.FromResult(new ResponseHttp
                    {
                        FailMessages = innerMessage,
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            }
        }
    }
}
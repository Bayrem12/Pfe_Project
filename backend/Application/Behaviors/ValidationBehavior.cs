using Application.Setting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Behaviors
{
    /// <summary>
    /// MediatR pipeline behavior that runs all registered FluentValidation validators
    /// for a request before it reaches the handler.
    ///
    /// For requests whose response type is ResponseHttp, validation failures are returned
    /// as a 400 Bad Request ResponseHttp. For other response types, a ValidationException
    /// is thrown (to be handled by global exception middleware).
    /// </summary>
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!_validators.Any())
                return await next();

            var context = new ValidationContext<TRequest>(request);

            var failures = _validators
                .Select(v => v.Validate(context))
                .SelectMany(r => r.Errors)
                .Where(e => e != null)
                .ToList();

            if (!failures.Any())
                return await next();

            // For commands that return ResponseHttp, return a structured 400 response
            if (typeof(TResponse) == typeof(ResponseHttp))
            {
                var message = "Validation Errors : " + string.Join("; ", failures.Select(e => e.ErrorMessage));
                var response = new ResponseHttp
                {
                    Status = StatusCodes.Status400BadRequest,
                    FailMessages = message
                };
                return (TResponse)(object)response;
            }

            // For other return types, throw — caught by global exception middleware
            throw new ValidationException(failures);
        }
    }
}

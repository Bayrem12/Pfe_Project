using Application.Setting;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace API.Extension
{
    /// <summary>
    /// Global exception handling middleware.
    /// Converts FluentValidation.ValidationException (thrown by the ValidationBehavior
    /// for non-ResponseHttp commands) into a structured 400 Bad Request response.
    /// </summary>
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlerMiddleware> _logger;

        public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";

                var message = "Validation Errors : " + string.Join("; ", ex.Errors.Select(e => e.ErrorMessage));
                var response = new ResponseHttp
                {
                    Status = StatusCodes.Status400BadRequest,
                    FailMessages = message
                };

                await context.Response.WriteAsJsonAsync(response);
            }
        }
    }
}

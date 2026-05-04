using Application.Features.CustomerFeatures.Validators;
using Application.Features.TestFeature.Commands;
using Application.Features.TestFeature.Queries;
using Application.Features.TestFeature.Validators;
using Application.Setting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
namespace API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class TestControllerNew : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<TestControllerNew> _logger;

        public TestControllerNew(IMediator mediator, ILogger<TestControllerNew> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult> Add(AddTestCommandNew cmd)
        {
            try
            {
                ResponseHttp AddCustomerResult;
                AddTestCommandNewValidator validator = new();

                AddCustomerResult = validator.Validate(new ValidationContext<AddTestCommandNew>(cmd));

                if (AddCustomerResult.Status == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(AddCustomerResult);
                }

                AddCustomerResult = await _mediator.Send(cmd);

                return Ok(AddCustomerResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        [HttpPut("")]
        public async Task<ActionResult> Update([FromBody] UpdateTestCommandNew cmd)
        {
            try
            {
                ResponseHttp updateCustomerResult;
                UpdateTestCommandNewValidator validator = new();

                updateCustomerResult = validator.Validate(new ValidationContext<UpdateTestCommandNew>(cmd));

                if (updateCustomerResult.Status == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(updateCustomerResult);
                }

                updateCustomerResult = await _mediator.Send(cmd);

                return Ok(updateCustomerResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }

        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<bool>> Delete(Guid id)
        {
            var result = await _mediator.Send(new DeleteTestCommandNew(id));
            return Ok(result);
        }
        [HttpGet("{id}")]
        public async Task<ActionResult> Get(Guid id)
        {
            GetTesByIdNewQuery qr = new(id);
            var result = await _mediator.Send(qr);

            return Ok(result);
        }
        [HttpGet("")]
        public async Task<ActionResult> Get(int? pageNumber, int? pageSize)
        {
            var result = await _mediator.Send(new GetAllTestNewQuery(pageNumber, pageSize));

            return Ok(result);
        }

    }
}

using Application.Features.TestSuitesFeature.Commands;
using Application.Features.TestSuitesFeature.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/testSuites")]
    [ApiController]
    [Authorize]
    public class TestSuitesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TestSuitesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Récupérer toutes les suites de tests d'un projet
        /// </summary>
        /// <param name="projectId">Identifiant du projet</param>
        /// <returns>Liste des suites de tests</returns>
        [HttpGet("by-project/{projectId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetByProjectId(Guid projectId)
        {
            var result = await _mediator.Send(new GetTestSuitesByProjectIdQuery(projectId));
            return Ok(result);
        }

        /// <summary>
        /// Récupérer une suite de tests par son identifiant
        /// </summary>
        /// <param name="id">Identifiant de la suite</param>
        /// <returns>Détails de la suite de tests</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _mediator.Send(new GetTestSuiteByIdQuery(id));
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        /// <summary>
        /// Récupérer une suite de tests avec ses scénarios ordonnés
        /// </summary>
        /// <param name="id">Identifiant de la suite</param>
        /// <returns>Suite de tests avec ses scénarios</returns>
        [HttpGet("{id}/with-cases")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetWithCases(Guid id)
        {
            var result = await _mediator.Send(new GetTestSuiteWithCasesQuery(id));
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        /// <summary>
        /// Mettre à jour une suite de tests
        /// </summary>
        /// <param name="id">Identifiant de la suite</param>
        /// <param name="command">Nouvelles informations</param>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTestSuiteCommand command)
        {
            if (id != command.Id)
            {
                return BadRequest("Route ID and body ID do not match.");
            }

            // ValidationBehavior in the MediatR pipeline handles input validation
            var updated = await _mediator.Send(command);
            if (!updated)
            {
                return NotFound();
            }
            return NoContent();
        }

        /// <summary>
        /// Créer une nouvelle suite de tests
        /// </summary>
        /// <param name="command">Informations de la suite de tests</param>
        /// <returns>Identifiant de la suite créée</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] AddTestSuiteCommand command)
        {
            try
            {
                var testSuiteId = await _mediator.Send(command);
                return Created($"api/TestSuites/{testSuiteId}", testSuiteId);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { fail_Messages = "Test suite already exists." });
            }
        }

        /// <summary>
        /// Supprimer une suite de tests (soft delete)
        /// </summary>
        /// <param name="id">Identifiant de la suite de tests</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _mediator.Send(new DeleteTestSuiteCommand(id));
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }

        /// <summary>
        /// Ajouter un scénario à une suite de tests
        /// </summary>
        /// <param name="suiteId">Identifiant de la suite de tests</param>
        /// <param name="scenarioId">Identifiant du scénario</param>
        [HttpPost("{suiteId}/scenarios/{scenarioId}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddScenarioToSuite(Guid suiteId, Guid scenarioId)
        {
            var result = await _mediator.Send(new AddScenarioToSuiteCommand(suiteId, scenarioId));
            if (!result)
            {
                return NotFound();
            }
            return Created($"api/TestSuites/{suiteId}/scenarios/{scenarioId}", null);
        }

        /// <summary>
        /// Retirer un scénario d'une suite de tests
        /// </summary>
        /// <param name="suiteId">Identifiant de la suite de tests</param>
        /// <param name="scenarioId">Identifiant du scénario</param>
        [HttpDelete("{suiteId}/scenarios/{scenarioId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveScenarioFromSuite(Guid suiteId, Guid scenarioId)
        {
            var result = await _mediator.Send(new RemoveScenarioFromSuiteCommand(suiteId, scenarioId));
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}

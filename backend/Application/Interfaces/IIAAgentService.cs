namespace Application.Interfaces
{
    /// <summary>
    /// Contract for calling the Python IA Test Agent microservice.
    /// </summary>
    public interface IIAAgentService
    {
        /// <summary>
        /// Sends a scenario to the IA Agent pipeline, persists the resulting
        /// TestExecution / TestResult / StepResult entities and returns the new execution Id.
        /// The target URL is extracted from the Gherkin steps (no external URL needed).
        /// </summary>
        /// <param name="scenarioId">The scenario to run.</param>
        /// <param name="executedById">The authenticated user who triggered the run.</param>
        /// <param name="isHeadless">When false the browser is visible; default true (headless).</param>
        /// <param name="ct">Cancellation token.</param>
        Task<Guid> RunScenarioAsync(
            Guid scenarioId,
            Guid executedById,
            bool isHeadless = true,
            CancellationToken ct = default);

        /// <summary>
        /// Runs every scenario in a test suite sequentially through the IA Agent.
        /// All results are stored under a single TestExecution record (with TestSuiteId set)
        /// so the suite run appears as one row in the Test Runs list.
        /// </summary>
        /// <param name="testSuiteId">The test suite to run.</param>
        /// <param name="executedById">The authenticated user who triggered the run.</param>
        /// <param name="isHeadless">When false the browser is visible; default true (headless).</param>
        /// <param name="ct">Cancellation token.</param>
        Task<Guid> RunTestSuiteAsync(
            Guid testSuiteId,
            Guid executedById,
            bool isHeadless = true,
            CancellationToken ct = default);

        /// <summary>
        /// Requests cancellation of a running AI execution.
        /// </summary>
        /// <param name="executionId">The execution to cancel.</param>
        Task<bool> CancelExecutionAsync(Guid executionId);
    }
}

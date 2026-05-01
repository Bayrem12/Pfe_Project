using Domain.Entities;
using Domain.Entities.Execution;
using Domain.Entities.Identity;
using Domain.Entities.NLP;
using Domain.Entities.ProjectManagement;
using Domain.Entities.Reporting;
using Domain.Entities.Scenarios;
using Domain.Entities.TestData;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces
{
    public interface ITestTestAutoumatisationContext : IContext
    {
        public DbSet<Test> Tests { get; set; }
        /// <summary>
        /// Execution
        /// </summary>
        public DbSet<ExecutionLog> ExecutionLogs { get; set; }
        public DbSet<Screenshot> Screenshots { get; set; }
        public DbSet<StepResult> StepResults { get; set; }
        public DbSet<TestExecution> TestExecutions { get; set; }
        public DbSet<TestResult> TestResults { get; set; }
        /// <summary>
        /// Identity
        /// </summary>
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        /// <summary>
        /// NLP
        /// </summary>
        public DbSet<ActionMapping> ActionMappings { get; set; }
        public DbSet<StepParameter> StepParameters { get; set; }
        public DbSet<StepAnalysis> StepAnalyses { get; set; }

        /// <summary>
        /// ProjectManagement
        /// </summary>
        public DbSet<Feature> Features { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        /// <summary>
        /// Reporting
        /// </summary>
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Report> Reports { get; set; }
        /// <summary>
        /// Scenarios
        /// </summary>
        public DbSet<Scenario> Scenarios { get; set; }
        public DbSet<ScenarioTag> ScenarioTags { get; set; }
        public DbSet<ScenarioVersion> ScenarioVersions { get; set; }
        public DbSet<Step> Steps { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<TestSuite> TestSuites { get; set; }
        public DbSet<TestSuiteScenario> TestSuiteScenarios { get; set; }
        /// <summary>
        /// TestData
        /// </summary>
        public DbSet<Domain.Entities.TestData.Environment> Environments { get; set; }


    }
}

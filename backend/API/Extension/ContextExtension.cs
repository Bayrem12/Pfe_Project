#nullable disable
using Application.Interfaces;
using Application.Interfaces.Identity;
using Application.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;
using Persistance.Repositories;
using Persistance.Repositories.Identity;
using Persistance.Services;

namespace API.Extension
{
    public static class ContextExtension
    {
        public static void ConfigureContext(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddDbContext<TestAutoumatisationContext>(options => options.UseNpgsql(GetConnectionInfo(configuration).ToString()).EnableSensitiveDataLogging());
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            // Register Data Access Layer — forward interface to the same AddDbContext instance
            services.AddScoped<ITestTestAutoumatisationContext>(sp => sp.GetRequiredService<TestAutoumatisationContext>());
            services.AddScoped<ITestRepository, TestRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IModulesRepository, ModulesRepository>();
            services.AddScoped<ITagsRepository, TagsRepository>();
            services.AddScoped<ITestSuiteRepository, TestSuiteRepository>();

            services.AddScoped<IProjectRepository, ProjectRepository>();

            
            services.AddScoped<IFeatureRepository, FeatureRepository>();
            // ===== NLP Feature Repositories =====
            // These repositories handle NLP-related database operations
            services.AddScoped<IStepAnalysisRepository, StepAnalysisRepository>();   // NLP step analysis CRUD
            services.AddScoped<IActionMappingRepository, ActionMappingRepository>(); // Intent → UI action mappings
            services.AddScoped<IScenarioRepository, ScenarioRepository>();           // Scenario with steps loading

            // ===== Dashboard Feature Repository =====
            // Aggregates data across multiple tables for dashboard statistics
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<IGherkinParserService, GherkinParserService>();
        }

        private static DbConnectionInfo GetConnectionInfo(IConfiguration configuration)
        {
            // Always read from configuration file first, fallback to environment variables
            var dbConnectionInfo = new DbConnectionInfo
            {
                Host = configuration.GetValue<string>("DataConnection:Hostname")
                       ?? Environment.GetEnvironmentVariable("PG_HOST"),
                Database = configuration.GetValue<string>("DataConnection:Database")
                           ?? Environment.GetEnvironmentVariable("PG_DATABASE"),
                Username = configuration.GetValue<string>("DataConnection:Username")
                           ?? Environment.GetEnvironmentVariable("PG_USERNAME"),
                Password = configuration.GetValue<string>("DataConnection:Password")
                           ?? Environment.GetEnvironmentVariable("PG_PASSWORD")
            };

            return dbConnectionInfo;
        }
    }
}

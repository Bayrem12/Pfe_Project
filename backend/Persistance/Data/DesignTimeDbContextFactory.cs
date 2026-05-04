using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Persistance.Data
{
    public class TestAutoumatisationContextFactory : IDesignTimeDbContextFactory<TestAutoumatisationContext>
    {
        public TestAutoumatisationContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestAutoumatisationContext>();

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "API"))
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var host = configuration["DataConnection:Hostname"];
            var database = configuration["DataConnection:Database"];
            var username = configuration["DataConnection:Username"];
            var password = configuration["DataConnection:Password"];

            var connectionString = $"Host={host};Database={database};Username={username};Password={password}";

            optionsBuilder.UseNpgsql(connectionString);

            return new TestAutoumatisationContext(optionsBuilder.Options);
        }
    }
}
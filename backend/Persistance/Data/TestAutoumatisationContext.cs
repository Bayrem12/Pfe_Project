#nullable disable
using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Entities.ComputerVision;
using Domain.Entities.Execution;
using Domain.Entities.Identity;
using Domain.Entities.NLP;
using Domain.Entities.ProjectManagement;
using Domain.Entities.Reporting;
using Domain.Entities.Scenarios;
using Domain.Entities.TestData;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Security.Claims;
using System.Text.Json;

namespace Persistance.Data
{
    public class TestAutoumatisationContext : DbContextBase , ITestTestAutoumatisationContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly Dictionary<Guid, string> _userDisplayNameCache = new();
        private readonly Dictionary<Guid, string> _roleNameCache = new();
        private readonly Dictionary<Guid, string> _projectNameCache = new();
        private readonly Dictionary<Guid, string> _scenarioTitleCache = new();
        private readonly Dictionary<Guid, string> _testSuiteNameCache = new();
        private readonly Dictionary<Guid, string> _actionMappingIntentCache = new();

        public TestAutoumatisationContext(
            DbContextOptions<TestAutoumatisationContext> options,
            IHttpContextAccessor? httpContextAccessor = null) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Test> Tests { get; set; }
        /// <summary>
        /// ComputerVision
        /// </summary>
        public DbSet<DetectionResult> DetectionResults { get; set; }
        public DbSet<UIElementCache> UIElementcaches { get; set; }
        public DbSet<UIPattern> UIPatterns { get; set; }
        /// <summary>
        /// Execution
        /// </summary>
        public DbSet<ExecutionLog> ExecutionLogs { get; set; }
        public DbSet<ExecutionSchedule> ExecutionSchedules { get; set; }
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
        public DbSet<EnvironmentVariable> EnvironmentVariables { get; set; }
        //public DbSet<Result<T>> Results { get; set; }
        public DbSet<TestDataEntry> TestDataEntries{ get; set; }
        public DbSet<TestDataSet> TestDataSets { get; set; }


        /// <summary>
        /// On model creating
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.SeedContext();
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestAutoumatisationContext).Assembly);
            modelBuilder.Entity<Test>().HasKey(t => new { t.Id});
            modelBuilder.Entity<StepResult>()
            .HasOne(sr => sr.Screenshot)
            .WithOne(s => s.StepResult)
            .HasForeignKey<Screenshot>(s => s.StepResultId)
            .OnDelete(DeleteBehavior.Cascade);

        }

        /// <summary>
        /// Called before save changes.
        /// </summary>
        protected override void OnBeforeSaveChanges()
        {
            AppendAuditLogsFromTrackedChanges();
            UseAuditable();
            UseSoftDelete();
            base.OnBeforeSaveChanges();
        }

        private void AppendAuditLogsFromTrackedChanges()
        {
            var userId = ResolveCurrentUserId();
            if (userId == Guid.Empty)
            {
                return;
            }

            var ipAddress = ResolveCurrentIpAddress();
            var now = DateTime.UtcNow;

            var entries = ChangeTracker
                .Entries<Entity>()
                .Where(e =>
                    e.Entity is not AuditLog &&
                    (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
                .ToList();

            if (!entries.Any())
            {
                return;
            }

            foreach (var entry in entries)
            {
                var isSoftDelete = IsSoftDeleteTransition(entry);
                var action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Modified when isSoftDelete => "Deleted",
                    EntityState.Modified => "Updated",
                    EntityState.Deleted => "Deleted",
                    _ => "Updated"
                };

                var oldValues = entry.State == EntityState.Added
                    ? null
                    : BuildPropertySnapshot(entry, useOriginalValues: true, onlyModifiedForUpdate: entry.State == EntityState.Modified);

                var newValues = entry.State == EntityState.Deleted
                    ? null
                    : BuildPropertySnapshot(entry, useOriginalValues: false, onlyModifiedForUpdate: entry.State == EntityState.Modified);

                // Avoid noisy empty updates where nothing effectively changed.
                if (action == "Updated" && oldValues is null && newValues is null)
                {
                    continue;
                }

                AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = action,
                    EntityType = entry.Metadata.ClrType.Name,
                    EntityId = entry.Entity.Id == Guid.Empty ? null : entry.Entity.Id,
                    OldValues = oldValues,
                    NewValues = newValues,
                    Timestamp = now,
                    IpAddress = ipAddress
                });
            }
        }

        private static bool IsSoftDeleteTransition(EntityEntry entry)
        {
            if (entry.State != EntityState.Modified)
            {
                return false;
            }

            var isDeletedProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            if (isDeletedProperty is null || !isDeletedProperty.IsModified)
            {
                return false;
            }

            var original = isDeletedProperty.OriginalValue as bool?;
            var current = isDeletedProperty.CurrentValue as bool?;

            return original == false && current == true;
        }

        private string? BuildPropertySnapshot(EntityEntry entry, bool useOriginalValues, bool onlyModifiedForUpdate)
        {
            var values = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsPrimaryKey())
                {
                    continue;
                }

                if (property.Metadata.IsShadowProperty())
                {
                    continue;
                }

                if (property.Metadata.Name is "CreatedDate" or "ModifiedDate" or "DeletedDate")
                {
                    continue;
                }

                if (onlyModifiedForUpdate && !property.IsModified)
                {
                    continue;
                }

                object? value;
                if (useOriginalValues)
                {
                    value = property.OriginalValue;
                }
                else
                {
                    value = property.CurrentValue;
                }

                values[property.Metadata.Name] = value;
            }

            EnrichSnapshotValues(entry, values, useOriginalValues);

            if (values.Count == 0)
            {
                return null;
            }

            return JsonSerializer.Serialize(values);
        }

        private void EnrichSnapshotValues(EntityEntry entry, Dictionary<string, object?> values, bool useOriginalValues)
        {
            var entityType = entry.Metadata.ClrType.Name;

            if (entityType == nameof(ProjectMember))
            {
                var memberUserId = ReadGuid(values, "UserId");
                if (memberUserId.HasValue)
                {
                    var userDisplayName = ResolveUserDisplayName(memberUserId.Value);
                    if (!string.IsNullOrWhiteSpace(userDisplayName))
                    {
                        values["UserDisplayName"] = userDisplayName;
                    }
                }

                var roleName = ResolveProjectRoleName(values);
                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    values["RoleName"] = roleName;
                }

                var projectId = ReadGuid(values, "ProjectId");
                if (projectId.HasValue)
                {
                    var projectName = ResolveProjectName(projectId.Value);
                    if (!string.IsNullOrWhiteSpace(projectName))
                    {
                        values["ProjectName"] = projectName;
                    }
                }
            }

            if (entityType == nameof(UserRole))
            {
                var roleUserId = ReadGuid(values, "UserId");
                if (roleUserId.HasValue)
                {
                    var userDisplayName = ResolveUserDisplayName(roleUserId.Value);
                    if (!string.IsNullOrWhiteSpace(userDisplayName))
                    {
                        values["UserDisplayName"] = userDisplayName;
                    }
                }

                var roleId = ReadGuid(values, "RoleId");
                if (roleId.HasValue)
                {
                    var roleName = ResolveRoleName(roleId.Value);
                    if (!string.IsNullOrWhiteSpace(roleName))
                    {
                        values["RoleName"] = roleName;
                    }
                }
            }

            if (entityType == nameof(TestSuiteScenario))
            {
                var scenarioId = ReadGuid(values, "ScenarioId");
                if (scenarioId.HasValue)
                {
                    var scenarioTitle = ResolveScenarioTitle(scenarioId.Value);
                    if (!string.IsNullOrWhiteSpace(scenarioTitle))
                    {
                        values["ScenarioTitle"] = scenarioTitle;
                    }
                }

                var testSuiteId = ReadGuid(values, "TestSuiteId");
                if (testSuiteId.HasValue)
                {
                    var testSuiteName = ResolveTestSuiteName(testSuiteId.Value);
                    if (!string.IsNullOrWhiteSpace(testSuiteName))
                    {
                        values["TestSuiteName"] = testSuiteName;
                    }
                }
            }

            if (entityType == nameof(ActionMapping))
            {
                if (!values.ContainsKey("IntentPattern") && entry.Entity is ActionMapping actionMapping)
                {
                    var intentPattern = ResolveActionMappingIntent(actionMapping.Id);
                    if (!string.IsNullOrWhiteSpace(intentPattern))
                    {
                        values["IntentPattern"] = intentPattern;
                    }
                }

                var projectId = ReadGuid(values, "ProjectId");
                if (projectId.HasValue)
                {
                    var projectName = ResolveProjectName(projectId.Value);
                    if (!string.IsNullOrWhiteSpace(projectName))
                    {
                        values["ProjectName"] = projectName;
                    }
                }
            }

            if (entityType == nameof(Scenario))
            {
                if (!values.ContainsKey("Title") && entry.Entity is Scenario scenario)
                {
                    var scenarioTitle = useOriginalValues
                        ? (entry.Property("Title").OriginalValue?.ToString() ?? ResolveScenarioTitle(scenario.Id))
                        : (entry.Property("Title").CurrentValue?.ToString() ?? ResolveScenarioTitle(scenario.Id));

                    if (!string.IsNullOrWhiteSpace(scenarioTitle))
                    {
                        values["Title"] = scenarioTitle;
                    }
                }
            }

            if (entityType == nameof(User))
            {
                var firstName = values.TryGetValue("FirstName", out var rawFirstName) ? rawFirstName?.ToString() ?? string.Empty : string.Empty;
                var lastName = values.TryGetValue("LastName", out var rawLastName) ? rawLastName?.ToString() ?? string.Empty : string.Empty;
                var displayName = $"{firstName} {lastName}".Trim();
                if (string.IsNullOrWhiteSpace(displayName) && entry.Entity is User userEntity)
                {
                    displayName = ResolveUserDisplayName(userEntity.Id);
                }

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    values["UserDisplayName"] = displayName;
                }
            }
        }

        private static Guid? ReadGuid(Dictionary<string, object?> values, string key)
        {
            if (!values.TryGetValue(key, out var raw) || raw is null)
            {
                return null;
            }

            if (raw is Guid guid)
            {
                return guid;
            }

            return Guid.TryParse(raw.ToString(), out var parsed) ? parsed : null;
        }

        private static string ResolveProjectRoleName(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue("Role", out var roleRaw) || roleRaw is null)
            {
                return string.Empty;
            }

            if (roleRaw is ProjectRole projectRole)
            {
                return projectRole.ToString();
            }

            if (int.TryParse(roleRaw.ToString(), out var enumValue) && Enum.IsDefined(typeof(ProjectRole), enumValue))
            {
                return ((ProjectRole)enumValue).ToString();
            }

            return roleRaw.ToString() ?? string.Empty;
        }

        private string ResolveUserDisplayName(Guid userId)
        {
            if (_userDisplayNameCache.TryGetValue(userId, out var cached))
            {
                return cached;
            }

            var tracked = ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == userId)?.Entity;

            if (tracked is not null)
            {
                var trackedName = $"{tracked.FirstName} {tracked.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(trackedName))
                {
                    _userDisplayNameCache[userId] = trackedName;
                    return trackedName;
                }

                if (!string.IsNullOrWhiteSpace(tracked.Email))
                {
                    _userDisplayNameCache[userId] = tracked.Email;
                    return tracked.Email;
                }
            }

            var dbUser = Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.FirstName, u.LastName, u.Email })
                .FirstOrDefault();

            if (dbUser is null)
            {
                return string.Empty;
            }

            var fullName = $"{dbUser.FirstName} {dbUser.LastName}".Trim();
            var result = !string.IsNullOrWhiteSpace(fullName) ? fullName : dbUser.Email;
            _userDisplayNameCache[userId] = result ?? string.Empty;
            return result ?? string.Empty;
        }

        private string ResolveRoleName(Guid roleId)
        {
            if (_roleNameCache.TryGetValue(roleId, out var cached))
            {
                return cached;
            }

            var tracked = ChangeTracker.Entries<Role>()
                .FirstOrDefault(e => e.Entity.Id == roleId)?.Entity;

            if (tracked is not null && !string.IsNullOrWhiteSpace(tracked.Name))
            {
                _roleNameCache[roleId] = tracked.Name;
                return tracked.Name;
            }

            var roleName = Roles
                .AsNoTracking()
                .Where(r => r.Id == roleId)
                .Select(r => r.Name)
                .FirstOrDefault();

            _roleNameCache[roleId] = roleName ?? string.Empty;
            return roleName ?? string.Empty;
        }

        private string ResolveProjectName(Guid projectId)
        {
            if (_projectNameCache.TryGetValue(projectId, out var cached))
            {
                return cached;
            }

            var tracked = ChangeTracker.Entries<Project>()
                .FirstOrDefault(e => e.Entity.Id == projectId)?.Entity;

            if (tracked is not null && !string.IsNullOrWhiteSpace(tracked.Name))
            {
                _projectNameCache[projectId] = tracked.Name;
                return tracked.Name;
            }

            var projectName = Projects
                .AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => p.Name)
                .FirstOrDefault();

            _projectNameCache[projectId] = projectName ?? string.Empty;
            return projectName ?? string.Empty;
        }

        private string ResolveScenarioTitle(Guid scenarioId)
        {
            if (_scenarioTitleCache.TryGetValue(scenarioId, out var cached))
            {
                return cached;
            }

            var tracked = ChangeTracker.Entries<Scenario>()
                .FirstOrDefault(e => e.Entity.Id == scenarioId)?.Entity;

            if (tracked is not null && !string.IsNullOrWhiteSpace(tracked.Title))
            {
                _scenarioTitleCache[scenarioId] = tracked.Title;
                return tracked.Title;
            }

            var scenarioTitle = Scenarios
                .AsNoTracking()
                .Where(s => s.Id == scenarioId)
                .Select(s => s.Title)
                .FirstOrDefault();

            _scenarioTitleCache[scenarioId] = scenarioTitle ?? string.Empty;
            return scenarioTitle ?? string.Empty;
        }

        private string ResolveTestSuiteName(Guid testSuiteId)
        {
            if (_testSuiteNameCache.TryGetValue(testSuiteId, out var cached))
            {
                return cached;
            }

            var tracked = ChangeTracker.Entries<TestSuite>()
                .FirstOrDefault(e => e.Entity.Id == testSuiteId)?.Entity;

            if (tracked is not null && !string.IsNullOrWhiteSpace(tracked.Name))
            {
                _testSuiteNameCache[testSuiteId] = tracked.Name;
                return tracked.Name;
            }

            var suiteName = TestSuites
                .AsNoTracking()
                .Where(s => s.Id == testSuiteId)
                .Select(s => s.Name)
                .FirstOrDefault();

            _testSuiteNameCache[testSuiteId] = suiteName ?? string.Empty;
            return suiteName ?? string.Empty;
        }

        private string ResolveActionMappingIntent(Guid actionMappingId)
        {
            if (_actionMappingIntentCache.TryGetValue(actionMappingId, out var cached))
            {
                return cached;
            }

            var tracked = ChangeTracker.Entries<ActionMapping>()
                .FirstOrDefault(e => e.Entity.Id == actionMappingId)?.Entity;

            if (tracked is not null && !string.IsNullOrWhiteSpace(tracked.IntentPattern))
            {
                _actionMappingIntentCache[actionMappingId] = tracked.IntentPattern;
                return tracked.IntentPattern;
            }

            var intent = ActionMappings
                .AsNoTracking()
                .Where(a => a.Id == actionMappingId)
                .Select(a => a.IntentPattern)
                .FirstOrDefault();

            _actionMappingIntentCache[actionMappingId] = intent ?? string.Empty;
            return intent ?? string.Empty;
        }

        private Guid ResolveCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private string ResolveCurrentIpAddress()
        {
            var context = _httpContextAccessor?.HttpContext;
            if (context is null)
            {
                return "unknown";
            }

            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstHop = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstHop))
                {
                    return NormalizeLoopback(firstHop);
                }
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIp))
            {
                return NormalizeLoopback(realIp.Trim());
            }

            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            return NormalizeLoopback(remoteIp ?? "unknown");
        }

        private static string NormalizeLoopback(string ip)
        {
            return ip == "::1" ? "127.0.0.1" : ip;
        }

    }
}

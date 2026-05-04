using Domain.Entities.Reporting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Persistance.Data;
using System.Security.Claims;

namespace API.Extension
{
    /// <summary>
    /// Writes audit log entries for successful non-GET API requests made by authenticated users.
    /// </summary>
    public class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLogMiddleware> _logger;

        public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (!ShouldLog(context, out var skipReason))
            {
                _logger.LogDebug("Audit skip for {Method} {Path}: {Reason}", context.Request.Method, context.Request.Path.Value, skipReason);
                return;
            }

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Audit skip for {Method} {Path}: missing/invalid user claim '{Claim}'", context.Request.Method, context.Request.Path.Value, userIdClaim ?? "null");
                return;
            }

            var request = context.Request;
            var entityType = ExtractEntityType(request.Path.Value);
            var entityId = ExtractEntityId(context);

            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = MapAction(request.Method),
                EntityType = entityType,
                EntityId = entityId,
                OldValues = null,
                NewValues = null,
                Timestamp = DateTime.UtcNow,
                IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            };

            try
            {
                using var scope = context.RequestServices.CreateScope();
                var auditDb = scope.ServiceProvider.GetRequiredService<TestAutoumatisationContext>();

                auditDb.AuditLogs.Add(auditLog);
                await auditDb.SaveChangesAsync();

                _logger.LogInformation(
                    "Audit persisted: {Action} {EntityType} ({EntityId}) by {UserId} from {Ip}",
                    auditLog.Action,
                    auditLog.EntityType,
                    auditLog.EntityId,
                    auditLog.UserId,
                    auditLog.IpAddress
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist audit log for path {Path}", request.Path.Value);
            }
        }

        private static bool ShouldLog(HttpContext context, out string reason)
        {
            var request = context.Request;
            if (!request.Path.StartsWithSegments("/api"))
            {
                reason = "non-api path";
                return false;
            }

            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                reason = "anonymous user";
                return false;
            }

            if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
            {
                reason = "read-only method";
                return false;
            }

            if (request.Path.StartsWithSegments("/api/dashboard/audit-logs"))
            {
                reason = "audit endpoint excluded";
                return false;
            }

            // We persist audit for any request that wasn't a server error.
            // This captures successful writes and validation failures for accountability.
            if (context.Response.StatusCode >= 500)
            {
                reason = $"server error status {context.Response.StatusCode}";
                return false;
            }

            reason = "ok";
            return true;
        }

        private static string MapAction(string method)
        {
            if (HttpMethods.IsPost(method)) return "Created";
            if (HttpMethods.IsPut(method) || HttpMethods.IsPatch(method)) return "Updated";
            if (HttpMethods.IsDelete(method)) return "Deleted";
            return method.ToUpperInvariant();
        }

        private static string ExtractEntityType(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "API";
            }

            var segments = path
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length >= 2)
            {
                return segments[1];
            }

            return "API";
        }

        private static Guid? ExtractEntityId(HttpContext context)
        {
            var routeValues = context.Request.RouteValues;
            var possibleKeys = new[] { "id", "projectId", "scenarioId", "featureId", "moduleId", "suiteId", "executionId", "userId" };

            foreach (var key in possibleKeys)
            {
                if (routeValues.TryGetValue(key, out var raw) && raw != null && Guid.TryParse(raw.ToString(), out var parsed))
                {
                    return parsed;
                }
            }

            var segments = context.Request.Path.Value?
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();

            foreach (var segment in segments)
            {
                if (Guid.TryParse(segment, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }
    }
}

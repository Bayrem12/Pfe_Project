using API.Extension;
using Application.Behaviors;
using Application.Interfaces;
using Application.Mappings;
using Asp.Versioning;
using Domain.Common;
using FluentValidation;
using Infrastructure.Services;
using Persistance.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Persistance.Data;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

IConfiguration configuration = builder.Configuration;

builder.Services.ConfigureContext(configuration, builder.Environment);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // ✅ Critique 6 — camelCase JSON pour convention Angular/JS frontend
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// ✅ Google OAuth
builder.Services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();

// ✅ GitHub OAuth
builder.Services.AddHttpClient<IGithubAuthService, GithubAuthService>();

// ✅ IA Test Agent – typed HttpClient with 5-minute timeout (tests can be slow)
builder.Services.AddHttpClient<IIAAgentService, IAAgentService>(client =>
{
    var baseUrl = builder.Configuration["IAAgent:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// ✅ Rate limiting — protection brute-force sur /auth/login
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("cors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "http://127.0.0.1:4200",
                "https://127.0.0.1:4200",
                "http://localhost",
                "http://localhost:80",
                "http://127.0.0.1"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMediatR(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

var applicationAssembly = typeof(Application.Features.NlpFeature.Validators.ParseGherkinCommandValidator).Assembly;
foreach (var type in applicationAssembly.GetTypes()
    .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition
             && t.GetInterfaces().Any(i => i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(IValidator<>))))
{
    var validatorInterface = type.GetInterfaces()
        .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));
    builder.Services.AddTransient(validatorInterface, type);
}

builder.Services.ConfigureSwagger();
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<AutoMapperProfiles>());
builder.Services.AddHttpContextAccessor();

// ✅ Critique 9 — API versioning par URL segment (/api/v1/...) et header Accept-Version
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("Accept-Version")
    );
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

var app = builder.Build();

// Apply EF Core migrations automatically on startup (safe to run repeatedly)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TestAutoumatisationContext>();
    await db.Database.MigrateAsync();
}

app.UseRouting();
app.UseMiddleware<ExceptionHandlerMiddleware>();

app.UseCors("cors");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ✅ Rate limiter — doit être après UseAuthorization
app.UseRateLimiter();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();
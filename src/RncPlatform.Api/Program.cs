using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RncPlatform.Application;
using RncPlatform.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Api.Startup;
using RncPlatform.Contracts.Responses;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RncPlatform.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

var builder = WebApplication.CreateBuilder(args);

var securitySettings = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();

// Configurar Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "RncPlatform.Api")
    .WriteTo.Console());

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins(
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://localhost:5173",
                    "https://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.SetIsOriginAllowed(_ => false);
    });
});

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSettings = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

if (jwtSettings.ExpiryMinutes <= 0)
{
    jwtSettings.ExpiryMinutes = 60;
}

if (jwtSettings.RefreshTokenExpiryDays <= 0)
{
    jwtSettings.RefreshTokenExpiryDays = 14;
}

if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    jwtSettings.SecretKey = $"dev-only-{Environment.MachineName}-rncplatform-jwt-secret-2026";
}

if (jwtSettings == null || string.IsNullOrEmpty(jwtSettings.SecretKey))
{
    throw new InvalidOperationException("La sección 'Jwt' en appsettings.json es inválida o no contiene 'SecretKey'.");
}

builder.Services.Configure<JwtOptions>(options =>
{
    options.SecretKey = jwtSettings.SecretKey;
    options.Issuer = jwtSettings.Issuer;
    options.Audience = jwtSettings.Audience;
    options.ExpiryMinutes = jwtSettings.ExpiryMinutes;
    options.RefreshTokenExpiryDays = jwtSettings.RefreshTokenExpiryDays;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var tokenVersionValue = context.Principal?.FindFirstValue(IdentityConstants.TokenVersionClaim);

            if (!Guid.TryParse(userIdValue, out var userId) || !int.TryParse(tokenVersionValue, out var tokenVersion))
            {
                context.Fail("El token no contiene las claims requeridas.");
                return;
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await userRepository.GetByIdAsync(userId, context.HttpContext.RequestAborted);

            if (user == null || !user.IsActive || user.TokenVersion != tokenVersion)
            {
                context.Fail("El token ya no es válido para este usuario.");
            }
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(IdentityConstants.AdminOnlyPolicy, policy =>
        policy.RequireRole(UserRole.Admin.ToString()));

    options.AddPolicy(IdentityConstants.CanManageUsersPolicy, policy =>
        policy.RequireRole(UserRole.Admin.ToString(), UserRole.UserManager.ToString()));

    options.AddPolicy(IdentityConstants.CanRunSyncPolicy, policy =>
        policy.RequireRole(UserRole.Admin.ToString(), UserRole.SyncOperator.ToString()));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Se excedió el límite de solicitudes. Intente de nuevo más tarde.",
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken: cancellationToken);
    };

    options.AddPolicy(IdentityConstants.LoginRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"login:{GetIpAddress(httpContext)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = securitySettings.LoginRateLimitPermitLimit,
                Window = TimeSpan.FromMinutes(securitySettings.LoginRateLimitWindowMinutes),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(IdentityConstants.RefreshRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"refresh:{GetUserOrIpPartitionKey(httpContext, "refresh")}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(IdentityConstants.UserManagementRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"user-management:{GetUserOrIpPartitionKey(httpContext, "users")}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(IdentityConstants.RncReadRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"rnc-read:{GetUserOrIpPartitionKey(httpContext, "rnc")}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(IdentityConstants.AdminSyncRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"admin-sync:{GetUserOrIpPartitionKey(httpContext, "sync")}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 4,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations();
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "RncPlatform API",
        Version = "v1",
        Description = "API para autenticacion, consulta de contribuyentes por RNC y operacion de sincronizacion DGII con soporte de roles, refresh tokens y paginacion por cursor."
    });

    var apiXmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var apiXmlPath = Path.Combine(AppContext.BaseDirectory, apiXmlFile);
    if (File.Exists(apiXmlPath))
    {
        c.IncludeXmlComments(apiXmlPath, includeControllerXmlComments: true);
    }

    var contractsXmlFile = $"{typeof(AuthResponse).Assembly.GetName().Name}.xml";
    var contractsXmlPath = Path.Combine(AppContext.BaseDirectory, contractsXmlFile);
    if (File.Exists(contractsXmlPath))
    {
        c.IncludeXmlComments(contractsXmlPath);
    }
    
    // JWT Bearer Definition
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando el esquema Bearer. Ejemplo: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" },
                Scheme = "Bearer",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Layers Dependency Injection
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Healthchecks
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "SQLServer", tags: ["ready"]);

var redisCs = builder.Configuration.GetConnectionString("Valkey");
if (!string.IsNullOrEmpty(redisCs))
{
    healthChecks.AddRedis(redisCs, name: "ValkeyCache", tags: ["ready"]);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RncPlatform API v1"));
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseCors("DefaultCors");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

await PrivilegedUserBootstrapper.EnsurePrivilegedUserAsync(app.Services, app.Configuration, app.Environment);

app.Run();

static string GetIpAddress(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string GetUserOrIpPartitionKey(HttpContext context, string prefix)
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"{prefix}:{userId}";
    }

    return $"{prefix}:{GetIpAddress(context)}";
}

public partial class Program;

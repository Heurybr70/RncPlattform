using Serilog;
using Microsoft.AspNetCore.Builder;
using RncPlatform.Application;
using RncPlatform.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Api.Middlewares;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Add services to the container.
builder.Services.AddControllers();

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSettings = jwtSection.Get<JwtOptions>();

if (jwtSettings == null || string.IsNullOrEmpty(jwtSettings.SecretKey))
{
    throw new InvalidOperationException("La sección 'Jwt' en appsettings.json es inválida o no contiene 'SecretKey'.");
}

builder.Services.Configure<JwtOptions>(jwtSection);

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
    };
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "RncPlatform API", Version = "v1" });
    
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
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "SQLServer");

var redisCs = builder.Configuration.GetConnectionString("Valkey");
if (!string.IsNullOrEmpty(redisCs))
{
    healthChecks.AddRedis(redisCs, name: "ValkeyCache");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RncPlatform API v1"));

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed User (Permanente para asegurar existencia en pruebas)
using (var scope = app.Services.CreateScope())
{
    var userRepo = scope.ServiceProvider.GetRequiredService<RncPlatform.Application.Abstractions.Persistence.IUserRepository>();
    var hasher = scope.ServiceProvider.GetRequiredService<RncPlatform.Application.Abstractions.Identity.IPasswordHasher>();
    var existing = await userRepo.GetByUsernameAsync("admin");
    if (existing == null)
    {
        await userRepo.AddAsync(new RncPlatform.Domain.Entities.User
        {
            Username = "admin",
            PasswordHash = hasher.HashPassword("RncPlatformAdmin2024!"),
            FullName = "Admin Principal",
            Email = "admin@rncplatform.com"
        });
        Console.WriteLine(">>>> USUARIO ADMIN CREADO EXITOSAMENTE <<<<");
    }
}

app.Run();

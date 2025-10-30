using System;
using CarSales.Api.Data;
using CarSales.Api.Controllers; // ApiKeyAuthFilter
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// ==========================
//     CONFIGURACIÓN API
// ==========================

// 1) Controllers
builder.Services.AddControllers();
// 1.1) Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Car Sales API",
        Version = "v1",
        Description = "API for querying and managing car sale listings."
    });

    // Define API Key security scheme (X-API-Key header)
    var apiKeyScheme = new OpenApiSecurityScheme
    {
        Description = "API key needed to access endpoints. Use the 'X-API-Key' header.",
        Name = "X-API-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    };

    options.AddSecurityDefinition("ApiKey", apiKeyScheme);

    // Require the API key for all operations (can be scoped per-operation if needed)
    // Use a reference to the security scheme so Swagger UI correctly applies the header
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                },
                In = ParameterLocation.Header,
                Name = "X-API-Key",
                Type = SecuritySchemeType.ApiKey
            },
            Array.Empty<string>()
        }
    });
});

// 2) Repositorio BD (lee la cadena de conexión de appsettings.json)
IConfiguration configuration = builder.Configuration;
string connectionString = configuration.GetConnectionString("CarSalesDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:CarSalesDb in appsettings.json");
builder.Services.AddScoped<ISalesRepository>(_ => new MySqlSalesRepository(connectionString));

// 3) Filtro de autenticación por API Key
builder.Services.AddScoped<ApiKeyAuthFilter>();

// ==========================
//      CONSTRUIR APP
// ==========================
var app = builder.Build();

// (Opcional) Si el HTTPS te da avisos, deja comentada la redirección
// app.UseHttpsRedirection();

// Configure Swagger middleware (expose OpenAPI and Swagger UI at /swagger)
// Expose Swagger in all environments for testing (adjust condition as needed)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    // Serve Swagger UI at /swagger
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Car Sales API v1");
});

app.MapControllers();

app.Run();

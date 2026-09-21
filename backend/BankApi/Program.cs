using BankApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Configuration de la base de données PostgreSQL
// ============================================================

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=postgres;Database=bankdb;Username=bankuser;Password=bankpassword";

builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseNpgsql(connectionString));


// ============================================================
// Authentification JWT
// ============================================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "dev-secret-key-change-me-in-production-please";

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
        ValidIssuer = "BankApi",

        ValidateAudience = true,
        ValidAudience = "BankApiClients",

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        ),

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // --------------------------------------------------------
    // Logs de diagnostic JWT
    // --------------------------------------------------------

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            Console.WriteLine("===== JWT MESSAGE RECEIVED =====");

            Console.WriteLine(
                $"Authorization: {context.Request.Headers.Authorization}"
            );

            return Task.CompletedTask;
        },

        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("===== JWT AUTHENTICATION FAILED =====");

            Console.WriteLine(
                $"Exception: {context.Exception.Message}"
            );

            Console.WriteLine(
                context.Exception.ToString()
            );

            return Task.CompletedTask;
        },

        OnTokenValidated = context =>
        {
            Console.WriteLine("===== JWT TOKEN VALIDATED =====");

            foreach (var claim in context.Principal!.Claims)
            {
                Console.WriteLine(
                    $"{claim.Type} = {claim.Value}"
                );
            }

            return Task.CompletedTask;
        },

        OnChallenge = context =>
        {
            Console.WriteLine("===== JWT CHALLENGE =====");

            Console.WriteLine(
                $"Error: {context.Error}"
            );

            Console.WriteLine(
                $"Description: {context.ErrorDescription}"
            );

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();


// ============================================================
// Services API
// ============================================================

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();


// ============================================================
// Swagger / OpenAPI
// ============================================================

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bank API",
        Version = "v1",
        Description = "Squelette d'API bancaire — .NET 8 / PostgreSQL"
    });

    // --------------------------------------------------------
    // Configuration JWT Bearer pour Swagger
    // --------------------------------------------------------

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",

        Type = SecuritySchemeType.Http,

        Scheme = "bearer",

        BearerFormat = "JWT",

        In = ParameterLocation.Header,

        Description = "Entrez votre token JWT."
    };

    c.AddSecurityDefinition(
        "Bearer",
        securityScheme
    );

    // --------------------------------------------------------
    // Indiquer à Swagger que les endpoints peuvent
    // utiliser le schéma Bearer
    // --------------------------------------------------------

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },

            Array.Empty<string>()
        }
    });
});


// ============================================================
// Intégration Odoo (ERP)
// ============================================================

builder.Services.Configure<BankApi.Integrations.Odoo.OdooOptions>(
    builder.Configuration.GetSection(
        BankApi.Integrations.Odoo.OdooOptions.SectionName
    )
);

builder.Services.AddHttpClient<
    BankApi.Integrations.Odoo.OdooClient
>();


// ============================================================
// Intégration Ollama (chatbot IA local)
// ============================================================

builder.Services.Configure<BankApi.Integrations.Ollama.OllamaOptions>(
    builder.Configuration.GetSection(
        BankApi.Integrations.Ollama.OllamaOptions.SectionName
    )
);

builder.Services.AddHttpClient<
    BankApi.Integrations.Ollama.OllamaClient
>();


// ============================================================
// Détection de fraude (ML.NET)
// ============================================================

builder.Services.AddSingleton<
    BankApi.Integrations.Fraud.FraudDetectionService
>();


// ============================================================
// Notifications (in-app + simulation d'envoi email)
// ============================================================

builder.Services.AddScoped<
    BankApi.Integrations.Notifications.NotificationService
>();


// ============================================================
// Simulateur de crédit
// ============================================================

builder.Services.Configure<BankApi.Integrations.Credit.CreditOptions>(
    builder.Configuration.GetSection(
        BankApi.Integrations.Credit.CreditOptions.SectionName
    )
);


// ============================================================
// CORS
// Autoriser le frontend React
// ============================================================

var frontendOrigin =
    builder.Configuration["FrontendOrigin"]
    ?? "http://localhost:3000";

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// ============================================================
// Construction de l'application
// ============================================================

var app = builder.Build();


// ============================================================
// Création automatique du schéma + seed au démarrage
// ============================================================

// IMPORTANT :
// EnsureCreated() est utilisé ici uniquement pour démarrer
// rapidement en local.
//
// En production, remplacer par de vraies migrations EF Core :
//
// dotnet ef migrations add InitialCreate
// dotnet ef database update
//
// EnsureCreated() et Migrate() ne doivent jamais être
// mélangés sur la même base.

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<BankDbContext>();

    db.Database.EnsureCreated();
}


// ============================================================
// Swagger
// ============================================================

app.UseSwagger();

app.UseSwaggerUI();


// ============================================================
// CORS
// ============================================================

app.UseCors("AllowFrontend");


// ============================================================
// Authentication / Authorization
// ============================================================

app.UseAuthentication();

app.UseAuthorization();


// ============================================================
// Controllers
// ============================================================

app.MapControllers();


// ============================================================
// Endpoint de santé pour Docker / monitoring
// ============================================================

app.MapGet(
    "/health",
    () => Results.Ok(
        new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        }
    )
);


// ============================================================
// Démarrage
// ============================================================

app.Run();

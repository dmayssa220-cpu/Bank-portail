using BankApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration de la base de données PostgreSQL ---
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=postgres;Database=bankdb;Username=bankuser;Password=bankpassword";

builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Authentification JWT ---
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-secret-key-change-me-in-production-please";

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// --- Services API ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Bank API",
        Version = "v1",
        Description = "Squelette d'API bancaire — .NET 8 / PostgreSQL"
    });

    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Collez ici uniquement le token JWT (sans le préfixe 'Bearer ')."
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// --- Intégration Odoo (ERP) ---
builder.Services.Configure<BankApi.Integrations.Odoo.OdooOptions>(
    builder.Configuration.GetSection(BankApi.Integrations.Odoo.OdooOptions.SectionName));
builder.Services.AddHttpClient<BankApi.Integrations.Odoo.OdooClient>();

// --- Intégration Ollama (chatbot IA local) ---
builder.Services.Configure<BankApi.Integrations.Ollama.OllamaOptions>(
    builder.Configuration.GetSection(BankApi.Integrations.Ollama.OllamaOptions.SectionName));
builder.Services.AddHttpClient<BankApi.Integrations.Ollama.OllamaClient>();

// --- Détection de fraude (ML.NET, entraîné sur données synthétiques au démarrage) ---
builder.Services.AddSingleton<BankApi.Integrations.Fraud.FraudDetectionService>();

// --- CORS : autoriser le frontend React ---
var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// --- Création automatique du schéma + seed au démarrage (pratique pour ce squelette) ---
// IMPORTANT : EnsureCreated() est utilisé ici uniquement pour démarrer rapidement en local.
// En production, remplacer par de vraies migrations EF Core versionnées :
//   dotnet ef migrations add InitialCreate
//   dotnet ef database update
// (EnsureCreated et Migrate ne doivent jamais être mélangés sur la même base)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Endpoint de santé pour Docker / monitoring
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

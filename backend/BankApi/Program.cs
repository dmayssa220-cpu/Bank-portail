using BankApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration de la base de données PostgreSQL ---
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=postgres;Database=bankdb;Username=bankuser;Password=bankpassword";

builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseNpgsql(connectionString));

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
app.UseAuthorization();
app.MapControllers();

// Endpoint de santé pour Docker / monitoring
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

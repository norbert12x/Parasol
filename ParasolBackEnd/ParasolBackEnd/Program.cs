using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using System.Reflection;
using ParasolBackEnd.Services;
using ParasolBackEnd.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Rejestracja serwisów
builder.Services.AddControllers();

// Pobierz folder z danymi
var dataFolder = builder.Configuration.GetValue<string>("DataFolder") ?? "dane";

// Dodaj usługę geolokalizacji z kluczem API
builder.Services.AddSingleton<IGeolocationService>(new GeolocationService("pk.8db67e501d12eeee6462b7332848ecd4", dataFolder));

// Dodaj OrganizacjaService z konfiguracją
builder.Services.AddSingleton<OrganizacjaService>(provider =>
    new OrganizacjaService(dataFolder, "pk.8db67e501d12eeee6462b7332848ecd4", 
        provider.GetService<ILogger<OrganizacjaService>>()!));

// KrsService TYMCZASOWO ZAKOMENTOWANY
// builder.Services.AddScoped<KrsService>(provider =>
//     new KrsService(provider.GetService<IDatabaseService>()!));

// Dodaj DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") + ";Multiplexing=false;Pooling=false;MinPoolSize=1;MaxPoolSize=1", 
        npgsqlOptions => npgsqlOptions
            .CommandTimeout(120) // 2 minuty timeout
            .EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null)
    ));

// Dodaj DatabaseService
builder.Services.AddScoped<IDatabaseService, DatabaseService>(provider =>
    new DatabaseService(
        provider.GetService<AppDbContext>()!,
        provider.GetService<ILogger<DatabaseService>>()!,
        builder.Configuration));

// Swagger z XML dokumentacją
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

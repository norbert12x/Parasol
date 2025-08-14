using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using System.Reflection;
using ParasolBackEnd.Services;

var builder = WebApplication.CreateBuilder(args);

// Rejestracja serwisw
builder.Services.AddControllers();

// Dodaj usug geolokalizacji z kluczem API
builder.Services.AddSingleton<IGeolocationService>(new GeolocationService("pk.8db67e501d12eeee6462b7332848ecd4"));

// Dodaj OrganizacjaService z konfiguracją
var dataFolder = builder.Configuration.GetValue<string>("DataFolder") ?? "dane";
builder.Services.AddSingleton<OrganizacjaService>(provider => 
    new OrganizacjaService(dataFolder, "pk.8db67e501d12eeee6462b7332848ecd4"));

// Dodaj KrsService z konfiguracją
builder.Services.AddSingleton<KrsService>(provider => 
    new KrsService(dataFolder));

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

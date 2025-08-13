using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParasolBackEnd.Services;

var builder = WebApplication.CreateBuilder(args);

// Rejestracja serwisów
builder.Services.AddControllers();

// Dodaj us³ugê geolokalizacji z kluczem API
builder.Services.AddSingleton<IGeolocationService>(new GeolocationService("pk.8db67e501d12eeee6462b7332848ecd4"));

// Dodaj OrganizacjaService
builder.Services.AddSingleton<OrganizacjaService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

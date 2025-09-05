using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using System.Reflection;
using ParasolBackEnd.Services;
using ParasolBackEnd.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Rejestracja serwisów
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

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

// Dodaj SecondDbContext dla MatchMaker
builder.Services.AddDbContext<SecondDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SecondDb") + ";Multiplexing=false;Pooling=false;MinPoolSize=1;MaxPoolSize=1", 
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

// Dodaj MatchMakerService
builder.Services.AddScoped<IMatchMakerService, MatchMakerService>();

// Dodaj AuthService
builder.Services.AddScoped<IAuthService, AuthService>();

// Konfiguracja JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"] ?? "default-key-change-in-production")),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ParasolBackEnd",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ParasolFrontEnd",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        
        // Niestandardowa obsługa błędów autoryzacji
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                
                var message = "Proszę się zalogować aby uzyskać dostęp do tej funkcji";
                if (context.Request.Path.StartsWithSegments("/api/matchmaker/posts"))
                {
                    message = "Proszę się zalogować aby utworzyć ogłoszenie";
                }
                
                var response = new { message = message, requiresAuth = true };
                await context.Response.WriteAsJsonAsync(response);
            }
        };
    });

builder.Services.AddAuthorization();

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
    
    // Dodaj konfigurację JWT dla Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MudBlazor.Services;
using qubic_spotlight.Components;
using qubic_spotlight.Endpoints;
using qubic_spotlight.Infrastructure;
using qubic_spotlight.Services;
using qubic_spotlight.Shared.Models;
using qubic_spotlight.Workers;

var builder = WebApplication.CreateBuilder(args);

// Blazor (WebAssembly-Render) + MudBlazor
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();

// Datenbank + Services
builder.Services.AddSingleton<LiteDbContext>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<GeoIpService>();
builder.Services.AddScoped<AdService>();
builder.Services.AddScoped<UserService>();

// Qubic-Stats: HttpClient + Hintergrund-Poller
builder.Services.AddHttpClient<QubicStatsClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["QubicRpc:BaseUrl"] ?? "https://rpc.qubic.org/");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<QubicStatsWorker>();

// Pool-Block-Kennzahlen (DOGE/LTC) von doge.qubic.tools: HttpClient + Poller
builder.Services.AddHttpClient<QubicBlockStatsClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["QubicBlocks:BaseUrl"] ?? "https://doge.qubic.tools/");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<QubicBlockStatsWorker>();

// Auth: JWT (Standard) + API-Key (zweites Schema)
var tokenService = new TokenService(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = tokenService.Issuer,
            ValidateAudience = true,
            ValidAudience = tokenService.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = tokenService.SigningKey,
            ValidateLifetime = true
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null);

builder.Services.AddAuthorization(options =>
{
    var schemes = new[] { JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthHandler.SchemeName };
    options.AddPolicy("ApiUser", p => p.AddAuthenticationSchemes(schemes).RequireAuthenticatedUser());
    options.AddPolicy("Manager", p => p.AddAuthenticationSchemes(schemes).RequireAuthenticatedUser()
        .RequireRole(Roles.Admin, Roles.Marketing));
    options.AddPolicy("Admin", p => p.AddAuthenticationSchemes(schemes).RequireAuthenticatedUser()
        .RequireRole(Roles.Admin));
});

// Swagger / OpenAPI mit Bearer- und ApiKey-Auth
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Qubic Spotlight API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT", Description = "JWT vom /api/auth/login"
    });
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthHandler.HeaderName, In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey, Description = "Langlebiger API-Key"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() },
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }, Array.Empty<string>() }
    });
});

// CORS offen: die öffentlichen Anzeigen werden bewusst überall eingebunden.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

var app = builder.Build();

// Erst-Admin anlegen, falls noch keine Benutzer existieren.
SeedAdmin(app);

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();
else
    app.UseResponseCompression();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Qubic Spotlight API v1"));

app.UseAntiforgery();
app.UseCors();

// Hochgeladene Bilder aus dem Volume unter /uploads ausliefern.
var uploadsDir = ApiEndpoints.UploadsDir(app.Environment);
Directory.CreateDirectory(uploadsDir);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsDir),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapApiEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(qubic_spotlight.Client._Imports).Assembly);

app.Run();

static void SeedAdmin(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LiteDbContext>();
    if (db.GetAllUsers().Count > 0) return;

    var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@qubic.local";
    var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "changeme";
    db.InsertUser(new User
    {
        Email = email,
        PasswordHash = PasswordHasher.Hash(password),
        Roles = new() { Roles.Admin }
    });
    app.Logger.LogInformation("Initialer Admin angelegt: {Email}", email);
}

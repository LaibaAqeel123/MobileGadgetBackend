using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Application.Services;
using MobileGadgets.Domain;
using MobileGadgets.Infrastructure.Auth;
using MobileGadgets.Infrastructure.Persistence;
using MobileGadgets.Infrastructure.Rendering;
using MobileGadgets.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<MobileGadgetsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("StorageSettings"));
builder.Services.AddScoped<IImageStorageService, LocalImageStorageService>();
builder.Services.AddScoped<IHeroModelRepository, HeroModelRepository>();
builder.Services.AddScoped<IHeroModelService, HeroModelService>();
builder.Services.AddScoped<IHeroImageRenderer, HeroImageRenderer>();
builder.Services.AddScoped<IHeroGenerationRepository, HeroGenerationRepository>();
builder.Services.AddScoped<IHeroGenerationService, HeroGenerationService>();
builder.Services.AddScoped<ISceneRepository, SceneRepository>();
builder.Services.AddScoped<ISceneService, SceneService>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IPasswordHasher, PasswordHasherService>();
builder.Services.AddScoped<IAuthTokenService, JwtAuthTokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

const string CorsPolicy = "FrontendCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// One-off CLI command: `dotnet run -- seed-admin --email=x --password=y` creates or promotes
// an Admin account, then exits without starting the web server. This is the only way an Admin
// account can ever be created — never through an HTTP endpoint — so there's no privilege-
// escalation surface. Mirrors phone-case-website's seed-admin command.
if (args.Length > 0 && args[0] == "seed-admin")
{
    string? email = null, password = null;
    foreach (var arg in args.Skip(1))
    {
        if (arg.StartsWith("--email=")) email = arg["--email=".Length..];
        else if (arg.StartsWith("--password=")) password = arg["--password=".Length..];
    }

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        Console.WriteLine("Usage: dotnet run -- seed-admin --email=you@example.com --password=yourpassword");
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MobileGadgetsDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (existing is not null)
    {
        existing.PasswordHash = hasher.Hash(password);
        existing.Role = UserRole.Admin;
        Console.WriteLine($"Updated existing user {email} -> Admin.");
    }
    else
    {
        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = hasher.Hash(password),
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow,
        });
        Console.WriteLine($"Created new Admin user {email}.");
    }

    await db.SaveChangesAsync();
    return;
}

// The static file provider is captured at startup: the uploads folder must exist
// before UseStaticFiles() runs, or it silently disables static serving for the
// whole process lifetime.
var uploadsPath = app.Configuration["StorageSettings:LocalBasePath"] ?? "wwwroot/uploads";
Directory.CreateDirectory(uploadsPath);

// wwwroot/uploads is per-environment (gitignored — it's user-uploaded content, not source), so
// the handful of background photos this system ships with (referenced by fixed filename in the
// Scene seed data above) are committed under SeedAssets instead and copied in here on every
// startup, in any environment, if not already present. Cheap no-op once they exist.
var seedAssetsPath = Path.Combine(AppContext.BaseDirectory, "SeedAssets", "backgrounds");
if (Directory.Exists(seedAssetsPath))
{
    foreach (var file in Directory.GetFiles(seedAssetsPath))
    {
        var dest = Path.Combine(uploadsPath, "seed-" + Path.GetFileName(file));
        if (!File.Exists(dest)) File.Copy(file, dest);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

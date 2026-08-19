using System.Text;
using AiAdmin.Api.Data;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<TokenService>();

var provider = builder.Configuration["Database:Provider"]?.Trim().ToLowerInvariant() ?? "sqlite";
var connectionString = builder.Configuration.GetConnectionString(provider)
                       ?? throw new InvalidOperationException($"Missing connection string for provider '{provider}'.");

builder.Services.AddDbContext<AppDbContext>(options =>
    {
        switch (provider) {
            case "sqlite":
                options.UseSqlite(connectionString);
                break;
            case "sqlserver":
                options.UseSqlServer(connectionString);
                break;
            case "postgresql":
            case "postgres":
                options.UseNpgsql(connectionString);
                break;
            case "mysql":
                options.UseMySQL(connectionString);
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider '{provider}'. Use sqlite, sqlserver, postgresql, or mysql.");
        }
    }
);

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true
                , ValidateAudience = true
                , ValidateLifetime = true
                , ValidateIssuerSigningKey = true
                , ValidIssuer = builder.Configuration["Jwt:Issuer"]
                , ValidAudience = builder.Configuration["Jwt:Audience"]
                , IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                , ClockSkew = TimeSpan.FromSeconds(30)
            };
        }
    );
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy(
        "Web", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? []).AllowAnyHeader().AllowAnyMethod()
    )
);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("Web");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await DatabaseInitializer.InitializeAsync(app.Services);
await app.RunAsync();
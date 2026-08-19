// 注册应用服务、认证授权、请求管道，并在启动时初始化和同步接口数据。

using System.Text;
using AiAdmin.Api.Data;
using AiAdmin.Api.Middleware;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ApiPermissionCache>();
builder.Services.AddScoped<ApiEndpointSyncService>();
builder.Services.AddScoped<TokenService>();

var provider = builder.Configuration["Database:Provider"]?.Trim().ToLowerInvariant() ?? "sqlite";
var connectionString = builder.Configuration.GetConnectionString(provider)
                       ?? throw new InvalidOperationException($"Missing connection string for provider '{provider}'.");

builder.Services.AddDbContext<AppDbContext>(options =>
    {
        _ = provider switch
        {
            "sqlite" => options.UseSqlite(connectionString)
            , "sqlserver" => options.UseSqlServer(connectionString)
            , "postgresql" or "postgres" => options.UseNpgsql(connectionString)
            , "mysql" => options.UseMySQL(connectionString)
            , _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'. Use sqlite, sqlserver, postgresql, or mysql.")
        };
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
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<ApiPermissionMiddleware>();
app.UseAuthorization();
app.MapControllers();

await DatabaseInitializer.InitializeAsync(app.Services).ConfigureAwait(false);
await using (var scope = app.Services.CreateAsyncScope()) {
    _ = await scope.ServiceProvider.GetRequiredService<ApiEndpointSyncService>().SyncAsync().ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);
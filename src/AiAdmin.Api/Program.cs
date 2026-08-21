using System.Text;
using AiAdmin.Api.Data;
using AiAdmin.Api.Middleware;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
_ = builder.Configuration.AddJsonFile("appsettings.Local.json", true, true);
SnowflakeIdGenerator.Configure(builder.Configuration.GetValue<long>("Snowflake:WorkerId", 0));

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new LongJsonConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DataAccessExceptionHandler>();
var redisConnection = builder.Configuration.GetConnectionString("Redis")
                      ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ApiPermissionCache>();
builder.Services.AddSingleton<DatabaseCommandAuditInterceptor>();
builder.Services.AddScoped<DataScopeContext>();
builder.Services.AddScoped<ApiEndpointSyncService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<MinioStorageService>();

var provider = builder.Configuration["Database:Provider"]?.Trim().ToLowerInvariant() ?? "sqlite";
var connectionString = builder.Configuration.GetConnectionString(provider)
                       ?? throw new InvalidOperationException($"Missing connection string for provider '{provider}'.");

builder.Services.AddDbContext<AppDbContext>((
        serviceProvider
        , options
    ) =>
    {
        _ = options.AddInterceptors(serviceProvider.GetRequiredService<DatabaseCommandAuditInterceptor>());
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
app.UseMiddleware<ResponseJsonCleanupMiddleware>();
app.UseMiddleware<DataScopeMiddleware>();
app.UseMiddleware<ApiPermissionMiddleware>();
app.UseAuthorization();
app.MapControllers();

await DatabaseInitializer.InitializeAsync(app.Services).ConfigureAwait(false);
await using (var scope = app.Services.CreateAsyncScope()) {
    _ = await scope.ServiceProvider.GetRequiredService<ApiEndpointSyncService>().SyncAsync().ConfigureAwait(false);
}

await DatabaseInitializer.InitializeRoleApisAsync(app.Services).ConfigureAwait(false);

await app.RunAsync().ConfigureAwait(false);
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AiAdmin.Api.Caching;
using AiAdmin.Api.Data;
using AiAdmin.Api.Logging;
using AiAdmin.Api.Middleware;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var applicationArgs = args.ToArray();
var builder = WebApplication.CreateBuilder(applicationArgs);
_ = builder.Configuration.AddJsonFile("appsettings.Local.json", true, true);
var replaceUrlArgumentIndex = Array.FindIndex(
    applicationArgs
    , argument => argument.Equals("--replace-url", StringComparison.OrdinalIgnoreCase)
                  || argument.StartsWith("--replace-url=", StringComparison.OrdinalIgnoreCase)
);
string? replaceUrl = null;
if (replaceUrlArgumentIndex >= 0) {
    if (applicationArgs[replaceUrlArgumentIndex].Contains('=', StringComparison.Ordinal)) {
        replaceUrl = applicationArgs[replaceUrlArgumentIndex]["--replace-url=".Length..];
    }
    else if (replaceUrlArgumentIndex + 1 < applicationArgs.Length) {
        replaceUrl = applicationArgs[replaceUrlArgumentIndex + 1];
    }
}

if (!string.IsNullOrWhiteSpace(replaceUrl)) {
    builder.Configuration["replace-url"] = replaceUrl;
}

SnowflakeIdGenerator.Configure(builder.Configuration.GetValue<long>("Snowflake:WorkerId", 0));
var provider = builder.Configuration["Database:Provider"]?.Trim().ToLowerInvariant() ?? "sqlserver";

// 在应用配置完成后立即输出运行环境信息，确保系统信息位于控制台首行
var startupEnvironmentVariables = string.Join(
    ", ", $"ASPNETCORE_ENVIRONMENT={Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "<not-set>"}"
    , $"DOTNET_ENVIRONMENT={Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "<not-set>"}"
    , $"ASPNETCORE_URLS={Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "<not-set>"}"
);
var entryAssembly = Assembly.GetEntryAssembly();
var assemblyVersion = entryAssembly?.GetName().Version?.ToString() ?? "<unknown>";
var informationalVersion = entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "<unknown>";
var configuredUrls = builder.WebHost.GetSetting("urls") ?? builder.Configuration["ASPNETCORE_URLS"] ?? "<pending>";
Console.WriteLine(
    "System information: "
    + $"MachineName={Environment.MachineName}, OS={RuntimeInformation.OSDescription}, OSArchitecture={RuntimeInformation.OSArchitecture}, "
    + $"ProcessArchitecture={RuntimeInformation.ProcessArchitecture}, Framework={RuntimeInformation.FrameworkDescription}, "
    + $"AssemblyVersion={assemblyVersion}, InformationalVersion={informationalVersion}, "
    + $"ProcessId={Environment.ProcessId}, ProcessorCount={Environment.ProcessorCount}, Environment={builder.Environment.EnvironmentName}, "
    + $"DatabaseProvider={provider}, ListeningUrls={configuredUrls}, StartupArguments={string.Join("|", args)}, "
    + $"EnvironmentVariables={startupEnvironmentVariables}"
);

builder.Logging.AddConsoleFormatter<AiAdminConsoleFormatter, ConsoleFormatterOptions>();
builder.Logging.AddConsole(options => options.FormatterName = "aiadmin");
builder.Logging.AddFilter("AiAdmin.Api.Middleware.ApiHttpLoggingMiddleware", LogLevel.Information);
builder.Logging.AddFilter("AiAdmin.Api.Services.ExternalHttpRequestService", LogLevel.Information);

_ = builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetJsonConverter());
            options.JsonSerializerOptions.Converters.Add(new LongJsonConverter());
        }
    );

builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
);
builder.Services.AddExceptionHandler<DataAccessExceptionHandler>();
builder.Services.Configure<ElasticsearchLogOptions>(builder.Configuration.GetSection("Elasticsearch"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ElasticsearchLogOptions>>().Value);
builder.Services.AddSingleton<ILoggerProvider, ElasticsearchLoggerProvider>();
builder.Services.AddHttpClient<ElasticsearchLogWriter>();
builder.Services.AddHttpClient<ElasticsearchLogQueryService>();
builder.Services.AddHttpClient<IpLocationService>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["IpLocation:BaseUrl"] ?? throw new InvalidOperationException("IpLocation:BaseUrl is required.")
        );
        client.Timeout = TimeSpan.FromSeconds(3);
    }
);
builder.Services.AddHostedService<ElasticsearchLogBackgroundService>();
var redisConnection = builder.Configuration.GetConnectionString("Redis")
                      ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = RedisKeyPrefix.VALUE;
    }
);

// 复用同一条 Redis 连接，并允许缓存管理读取服务器级 INFO/DBSIZE 等指标
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var redisOptions = ConfigurationOptions.Parse(redisConnection);
        redisOptions.AllowAdmin = true;
        return ConnectionMultiplexer.Connect(redisOptions);
    }
);
builder.Services.AddSingleton<ElasticsearchLogQueue>();
builder.Services.AddSingleton<ScheduledJobLockService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ApiPermissionCache>();
builder.Services.AddSingleton<DatabaseCommandAuditInterceptor>();
builder.Services.AddScoped<DataScopeContext>();
builder.Services.AddScoped<ApiEndpointSyncService>();
builder.Services.AddScoped<ApiDocumentationService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<DictionarySnapshotService>();
builder.Services.AddScoped<ExportLimitService>();
builder.Services.AddSingleton<MinioStorageService>();
builder.Services.AddHttpClient();
builder.Services.AddTransient<ExternalHttpRequestService>();
builder.Services.AddHostedService<ScheduledJobHostedService>();
builder.Services.AddHostedService<ScheduledJobReleaseHostedService>();

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
app.UseForwardedHeaders();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<ApiHttpLoggingMiddleware>();
app.UseMiddleware<ResponseJsonCleanupMiddleware>();
app.UseMiddleware<DataScopeMiddleware>();
app.UseMiddleware<ApiPermissionMiddleware>();
app.UseAuthorization();
app.MapControllers();

// 仅开发和测试环境自动初始化数据库，其他环境不自动执行初始化
var shouldInitializeDatabase = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test");
if (shouldInitializeDatabase) {
    var shouldClearDatabase = args.Any(argument =>
        string.Equals(argument, "--clear-database", StringComparison.OrdinalIgnoreCase)
        || string.Equals(argument, "--delete-all-tables", StringComparison.OrdinalIgnoreCase)
    );
    if (shouldClearDatabase) {
        await DatabaseInitializer.DeleteAllTablesAsync(app.Services).ConfigureAwait(false);
    }

    // 系统数据与业务种子分阶段初始化，便于按需重建业务数据而不影响系统配置
    await DatabaseInitializer.InitializeSystemDataAsync(app.Services).ConfigureAwait(false);
    await DatabaseInitializer.InitializeBusinessDataAsync(app.Services).ConfigureAwait(false);
    await using (var scope = app.Services.CreateAsyncScope()) {
        _ = await scope.ServiceProvider.GetRequiredService<ApiEndpointSyncService>().SyncAsync().ConfigureAwait(false);
        var dictionarySnapshotService = scope.ServiceProvider.GetRequiredService<DictionarySnapshotService>();
        await dictionarySnapshotService.RefreshAsync(DictionarySnapshotService.SYSTEM_SETTINGS_CODE).ConfigureAwait(false);
        await dictionarySnapshotService.RefreshAsync(DictionarySnapshotService.SCHEDULED_JOB_PLACEHOLDERS_CODE).ConfigureAwait(false);
    }

    await DatabaseInitializer.InitializeRoleApisAsync(app.Services).ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);
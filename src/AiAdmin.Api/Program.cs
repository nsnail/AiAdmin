using System.Collections;
using System.Globalization;
using System.Reflection;
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

// 仅在显式启用后台服务时注册相关后台服务
var enabledHostedServices = applicationArgs.Any(argument => argument.Equals("--enabled-hosted-services", StringComparison.OrdinalIgnoreCase));
var replaceUrlArgumentIndex = Array.FindIndex(
    applicationArgs
    , argument => argument.Equals("--replace-job-request-url", StringComparison.OrdinalIgnoreCase)
                  || argument.StartsWith("--replace-job-request-url=", StringComparison.OrdinalIgnoreCase)
);
string? replaceUrl = null;
if (replaceUrlArgumentIndex >= 0) {
    if (applicationArgs[replaceUrlArgumentIndex].Contains('=', StringComparison.Ordinal)) {
        replaceUrl = applicationArgs[replaceUrlArgumentIndex]["--replace-job-request-url=".Length..];
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
var entryAssembly = Assembly.GetEntryAssembly();
var assemblyName = entryAssembly!.GetName().ToString();
Console.WriteLine($"{new string('-', 4)} {assemblyName} {new string('-', Math.Max(120 - 5 - assemblyName.Length, 1))}");
foreach (var kv in GetEnvironmentInfo().OrderBy(x => x.Key)) {
    Console.WriteLine($"<{kv.Key}> {kv.Value}");
}

Console.WriteLine(new string('-', 120));

builder.Logging.AddConsoleFormatter<AiAdminConsoleFormatter, ConsoleFormatterOptions>();
builder.Logging.AddConsole(options => options.FormatterName = "aiadmin");
builder.Logging.AddFilter("AiAdmin.Api.Middleware.ApiHttpLoggingMiddleware", LogLevel.Information);
builder.Logging.AddFilter("AiAdmin.Api.Services.ExternalHttpRequestService", LogLevel.Information);

_ = builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
        {
            // 所有控制器请求统一忽略 JSON 属性名大小写
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetJsonConverter());
            options.JsonSerializerOptions.Converters.Add(new LongJsonConverter());
            options.JsonSerializerOptions.MaxDepth = 128;
        }
    );
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
);
builder.Services.AddExceptionHandler<DataAccessExceptionHandler>();
builder.Services.Configure<ElasticsearchLogOptions>(builder.Configuration.GetSection("Elasticsearch"));
builder.Services.Configure<ApiLogStorageOptions>(builder.Configuration.GetSection("ApiLogStorage"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ApiLogStorageOptions>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ElasticsearchLogOptions>>().Value);
builder.Services.AddSingleton<ILoggerProvider, ElasticsearchLoggerProvider>();
builder.Services.Configure<FileLogOptions>(builder.Configuration.GetSection("FileLogging"));
builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();

// 统一配置出站 HttpClient，兼容 GmailCheck 等外部服务使用异常或不受信任 SSL 证书的场景
// 该设置会跳过所有由 IHttpClientFactory 创建的客户端的服务器证书校验
builder.Services.ConfigureHttpClientDefaults(httpClientBuilder => httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }
    )
);

builder.Services.AddHttpClient<ElasticsearchLogWriter>();
builder.Services.AddScoped<ElasticsearchLogQueryService>();
builder.Services.AddHttpClient<IpLocationService>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["IpLocation:BaseUrl"] ?? throw new InvalidOperationException("IpLocation:BaseUrl is required.")
        );
        client.Timeout = TimeSpan.FromSeconds(3);
    }
);
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
builder.Services.AddSingleton<ApiLogStorageQueue>();
builder.Services.AddSingleton<ScheduledJobLockService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ApiPermissionCache>();
builder.Services.AddSingleton<SystemInfoService>();
builder.Services.AddSingleton<IRealtimeBus, RedisRealtimeBus>();
builder.Services.AddSingleton<RealtimeConnectionManager>();
builder.Services.AddHostedService<RealtimeShutdownHostedService>();

// 停机超时需小于容器 stop_grace_period，确保主机先于 SIGKILL 完成优雅停止
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(40));
builder.Services.AddSingleton<DatabaseCommandAuditInterceptor>();
builder.Services.AddSingleton<DatabaseShutdownInterceptor>();
builder.Services.AddScoped<DataScopeContext>();
builder.Services.AddSingleton<DataScopeCache>();
builder.Services.AddScoped<ApiEndpointSyncService>();
builder.Services.AddScoped<ApiDocumentationService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<DictionarySnapshotService>();
builder.Services.AddScoped<ExportLimitService>();
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddSingleton<CredentialProtectionService>();
builder.Services.AddSingleton<MinioStorageService>();
builder.Services.AddHttpClient();
builder.Services.AddTransient<ExternalHttpRequestService>();
if (enabledHostedServices) {
    _ = builder.Services.AddHostedService<ScheduledJobHostedService>();
    _ = builder.Services.AddHostedService<ScheduledJobReleaseHostedService>();
    _ = builder.Services.AddHostedService<ElasticsearchLogBackgroundService>();
    _ = builder.Services.AddHostedService<ApiLogStorageBackgroundService>();
}

var connectionString = builder.Configuration.GetConnectionString(provider)
                       ?? throw new InvalidOperationException($"Missing connection string for provider '{provider}'.");

// 请求日志按天分表，建表、批量写入和删除过期分表都使用与主库相同的提供方和连接串
builder.Services.AddSingleton(new ApiRequestLogTableService(provider, connectionString));

builder.Services.AddDbContext<AppDbContext>((
        serviceProvider
        , options
    ) =>
    {
        _ = options.AddInterceptors(
            serviceProvider.GetRequiredService<DatabaseCommandAuditInterceptor>(), serviceProvider.GetRequiredService<DatabaseShutdownInterceptor>()
        );
        _ = provider switch
        {
            "sqlite" => options.UseSqlite(
                connectionString, sqliteOptions => sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            , "sqlserver" => options.UseSqlServer(
                connectionString, sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            , "postgresql" or "postgres" => options.UseNpgsql(
                connectionString, npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            , "mysql" => options.UseMySQL(connectionString, mysqlOptions => mysqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            , _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'. Use sqlite, sqlserver, postgresql, or mysql.")
        };
    }
);

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
        {
            // 浏览器 WebSocket 无法携带请求头，实时连接端点允许通过查询参数传递令牌
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Path.Equals(RealtimeWebSocketMiddleware.PATH, StringComparison.OrdinalIgnoreCase)
                        && context.Request.Query.TryGetValue("access_token", out var token)) {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                }
            };
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

app.UseMiddleware<ShutdownGateMiddleware>();
app.UseExceptionHandler();
app.UseCors("Web");
app.UseForwardedHeaders();
app.UseRouting();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.UseAuthentication();
app.UseMiddleware<RealtimeWebSocketMiddleware>();
app.UseMiddleware<ApiHttpLoggingMiddleware>();
app.UseMiddleware<ResponseJsonCleanupMiddleware>();
app.UseMiddleware<DataScopeMiddleware>();
app.UseMiddleware<ApiPermissionMiddleware>();
app.UseAuthorization();
app.MapControllers();

// 健康检查：匿名可访问，不在 /api 下因此不产生请求日志；安全停机闸门关闭后由 ShutdownGateMiddleware 直接返回 503，网关据此摘除实例
_ = app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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
return;

static Dictionary<string, object?> GetEnvironmentInfo() {
    var ret = typeof(Environment)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(x => x.Name is not (nameof(Environment.StackTrace) or nameof(Environment.NewLine)))
        .ToDictionary(x => x.Name, x => x.GetValue(null));

    var vars = Environment.GetEnvironmentVariables();
    var keys = new ArrayList(vars.Keys);
    keys.Sort();
    var sb = new StringBuilder(vars.Count);
    foreach (var key in keys) {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{key}: {vars[key]}");
    }

    _ = ret.TryAdd("EnvironmentVars", sb.ToString().Trim());
    return ret;
}
namespace AiAdmin.Api.Data;

/// <summary>
///     业务数据种子初始化器
/// </summary>
internal static class BusinessDataSeeder
{
    /// <summary>
    ///     初始化业务数据
    /// </summary>
    /// <param name="services">应用服务提供器</param>
    /// <returns>异步初始化任务</returns>
    public static async Task InitializeAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _ = await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }
}
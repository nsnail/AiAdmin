using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AiAdmin.Api.Data;

/// <summary>
///     单个日分表的数据库上下文，仅用于借助 EF 生成建表语句，表名随日期变化，不参与应用主模型
/// </summary>
/// <param name="options">数据库上下文配置</param>
/// <param name="tableName">日分表名称</param>
public sealed class ApiRequestLogContext(DbContextOptions<ApiRequestLogContext> options, string tableName) : DbContext(options)
{
    /// <summary>
    ///     日分表中的请求日志，表名由构造参数决定
    /// </summary>
    public DbSet<ApiRequestLog> Logs => Set<ApiRequestLog>();

    /// <summary>
    ///     日分表名称，同时作为模型缓存键的一部分
    /// </summary>
    public string TableName { get; } = tableName;

    /// <summary>
    ///     配置日分表的表名、主键、字段长度和索引
    /// </summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        _ = modelBuilder.Entity<ApiRequestLog>(entity =>
            {
                _ = entity.ToTable(TableName);
                _ = entity.HasKey(x => x.Id);
                _ = entity.Property(x => x.Id).ValueGeneratedNever();
                _ = entity.Property(x => x.Method).HasConversion<byte>();

                // 千万级大表只建查询必需的索引，每多一个索引都会增加写入开销和存储空间
                _ = entity.HasIndex(x => x.CreatedAt);
                _ = entity.HasIndex(x => x.UserId);
                _ = entity.HasIndex(x => x.PathCrc32);
                _ = entity.HasIndex(x => x.TraceId);
            }
        );
    }
}

/// <summary>
///     让每个日分表使用独立的 EF 模型缓存，避免不同表名共用同一份模型
/// </summary>
public sealed class ApiRequestLogModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <summary>
    ///     生成包含表名的模型缓存键
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <param name="designTime">是否为设计时模型</param>
    /// <returns>模型缓存键</returns>
    public object Create(
        DbContext context
        , bool designTime
    ) {
        return context is ApiRequestLogContext logContext ? (context.GetType(), logContext.TableName, designTime) : (context.GetType(), designTime);
    }
}
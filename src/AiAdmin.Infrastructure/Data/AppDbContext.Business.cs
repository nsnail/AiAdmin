using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Data;

/// <summary>
///     应用数据库上下文的业务部分，承载业务管理（账号、设备、任务）的实体集合、模型配置和保存前处理，
///     与框架部分分文件维护，减少与框架代码合并时的冲突和审查范围
/// </summary>
public sealed partial class AppDbContext
{
    /// <summary>
    ///     配置业务实体的表名、字段和索引
    /// </summary>
    /// <param name="modelBuilder">实体模型构建器</param>
    static partial void ConfigureBusinessModel(ModelBuilder modelBuilder) {
        // ignore
    }

    /// <summary>
    ///     保存前按任务状态同步所有新增和修改任务的非终态唯一键
    /// </summary>
    static partial void PrepareBusinessSaveChanges() {
        // ingore
    }
}
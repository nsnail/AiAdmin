using AiAdmin.Api.Contracts;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     启用状态管理控制器的业务部分，处理业务管理资源的启用状态，
///     与框架部分分文件维护，减少与框架代码合并时的冲突和审查范围
/// </summary>
public sealed partial class EnabledStatesController
{
    /// <summary>
    ///     判断资源是否使用字符串主键，账号以手机号为主键，设备以设备编号为主键
    /// </summary>
    /// <param name="resource">资源类型</param>
    /// <returns>使用字符串主键时为 true</returns>
    private static partial bool IsStringKeyResource(string resource) {
        return false;
    }

    /// <summary>
    ///     更新业务资源的启用状态
    /// </summary>
    /// <param name="request">启用状态请求</param>
    /// <returns>受影响的行数，资源类型不受支持时为 -1</returns>
    private static partial Task<int> UpdateBusinessEnabledStateAsync(ResourceEnabledRequest request) {
        return Task.FromResult(-1);
    }
}
namespace AiAdmin.Api.Models;

/// <summary>
///     限流算法
/// </summary>
public enum RateLimitAlgorithm
{
    /// <summary>
    ///     滑动窗口，任意连续窗口时间内的请求数不超过上限
    /// </summary>
    SlidingWindow = 1

    ,

    /// <summary>
    ///     漏桶，请求入桶后以恒定速率流出，桶满时拒绝，输出速率平滑
    /// </summary>
    LeakyBucket = 2

    ,

    /// <summary>
    ///     令牌桶，按恒定速率补充令牌，请求消耗令牌，允许桶容量内的突发流量
    /// </summary>
    TokenBucket = 3
}
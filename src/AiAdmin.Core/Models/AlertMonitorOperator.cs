namespace AiAdmin.Api.Models;

/// <summary>
///     告警监控的比较操作符，数字和字符串共用一套编号：前六个可用于数字，字符串支持相等、不相等和后六个文本操作符
/// </summary>
public enum AlertMonitorOperator
{
    /// <summary>
    ///     大于，仅数字
    /// </summary>
    GreaterThan = 1

    ,

    /// <summary>
    ///     大于等于，仅数字
    /// </summary>
    GreaterThanOrEqual = 2

    ,

    /// <summary>
    ///     小于，仅数字
    /// </summary>
    LessThan = 3

    ,

    /// <summary>
    ///     小于等于，仅数字
    /// </summary>
    LessThanOrEqual = 4

    ,

    /// <summary>
    ///     等于，数字和字符串
    /// </summary>
    Equal = 5

    ,

    /// <summary>
    ///     不等于，数字和字符串
    /// </summary>
    NotEqual = 6

    ,

    /// <summary>
    ///     包含，仅字符串
    /// </summary>
    Contains = 7

    ,

    /// <summary>
    ///     不包含，仅字符串
    /// </summary>
    NotContains = 8

    ,

    /// <summary>
    ///     以指定文本开头，仅字符串
    /// </summary>
    StartsWith = 9

    ,

    /// <summary>
    ///     以指定文本结尾，仅字符串
    /// </summary>
    EndsWith = 10

    ,

    /// <summary>
    ///     匹配正则表达式，仅字符串，阈值为正则表达式
    /// </summary>
    RegexMatch = 11

    ,

    /// <summary>
    ///     不匹配正则表达式，仅字符串，阈值为正则表达式
    /// </summary>
    RegexNotMatch = 12
}
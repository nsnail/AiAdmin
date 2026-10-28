namespace AiAdmin.Api.Contracts;

/// <summary>
///     仪表盘接口请求占比的一个扇区
/// </summary>
/// <param name="Path">接口路径，接口表里找不到时为路径 CRC32 的数值文本，合并的其他项为空</param>
/// <param name="Name">接口说明的英文原文，由前端按当前语言翻译，找不到接口或合并的其他项为空</param>
/// <param name="Value">请求数</param>
public sealed record DashboardApiShare(string Path, string Name, int Value);
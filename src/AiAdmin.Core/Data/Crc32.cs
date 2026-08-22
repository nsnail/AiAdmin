#pragma warning disable SA1518

using System.Text;

namespace AiAdmin.Api.Data;

/// <summary>
///     计算文本 CRC32 校验值
/// </summary>
public static class Crc32
{
    private static readonly uint[] _table = CreateTable();

    /// <summary>
    ///     计算 UTF-8 文本的 CRC32
    /// </summary>
    /// <param name="value">待计算文本</param>
    /// <returns>有符号 CRC32 整数</returns>
    public static int Compute(string value) {
        var crc = uint.MaxValue;
        foreach (var item in Encoding.UTF8.GetBytes(value)) {
            crc = _table[(crc ^ item) & byte.MaxValue] ^ (crc >> 8);
        }

        return unchecked((int)~crc);
    }

    private static uint[] CreateTable() {
        const uint polynomial = 0xedb88320;
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++) {
            var value = index;
            for (var bit = 0; bit < 8; bit++) {
                value = (value & 1) == 0 ? value >> 1 : (value >> 1) ^ polynomial;
            }

            table[index] = value;
        }

        return table;
    }
}
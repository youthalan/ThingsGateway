﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

namespace ThingsGateway.Foundation.SiemensS7;

/// <summary>
/// https://github.com/S7NetPlus/s7netplus/blob/develop/S7.Net/Types/DateTime.cs
/// Contains the methods to convert between <see cref="System.DateTime"/> and S7 representation of datetime values.
/// </summary>
public static class S7DateTime
{
    /// <summary>
    /// The maximum <see cref="System.DateTime"/> value supported by the specification.
    /// </summary>
    public static readonly System.DateTime SpecMaximumDateTime = new(2089, 12, 31, 23, 59, 59, 999);

    /// <summary>
    /// The minimum <see cref="System.DateTime"/> value supported by the specification.
    /// </summary>
    public static readonly System.DateTime SpecMinimumDateTime = new(1990, 1, 1);

    /// <summary>
    /// Parses a <see cref="System.DateTime"/> value from bytes.
    /// </summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>A <see cref="System.DateTime"/> object representing the value read from PLC.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the length of
    ///   <paramref name="bytes"/> is not 8 or any value in <paramref name="bytes"/>
    ///   is outside the valid range of values.</exception>
    public static System.DateTime FromByteArray(this byte[]? bytes)
    {
        return FromByteArrayImpl(bytes);
    }

    /// <summary>
    /// Parses an array of <see cref="System.DateTime"/> values from bytes.
    /// </summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>An array of <see cref="System.DateTime"/> objects representing the values read from PLC.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the length of
    ///   <paramref name="bytes"/> is not a multiple of 8 or any value in
    ///   <paramref name="bytes"/> is outside the valid range of values.</exception>
    public static System.DateTime[] ToArray(byte[] bytes)
    {
        if (bytes.Length % 8 != 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length,
                $"Parsing an array of DateTime requires a multiple of 8 bytes of input data, input data is '{bytes.Length}' long.");

        var cnt = bytes.Length / 8;
        var result = new System.DateTime[bytes.Length / 8];

        for (var i = 0; i < cnt; i++)
            result[i] = FromByteArrayImpl(new ArraySegment<byte>(bytes, i * 8, 8));

        return result;
    }

    /// <summary>
    /// Converts a <see cref="System.DateTime"/> value to a byte array.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <returns>A byte array containing the S7 date time representation of <paramref name="dateTime"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value of
    ///   <paramref name="dateTime"/> is before <see cref="SpecMinimumDateTime"/>
    ///   or after <see cref="SpecMaximumDateTime"/>.</exception>
    public static byte[] ToByteArray(this System.DateTime dateTime)
    {
        byte EncodeBcd(int value)
        {
            return (byte)((value / 10 << 4) | value % 10);
        }

        if (dateTime < SpecMinimumDateTime)
            throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime,
                $"Date time '{dateTime}' is before the minimum '{SpecMinimumDateTime}' supported in S7 date time representation.");

        if (dateTime > SpecMaximumDateTime)
            throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime,
                $"Date time '{dateTime}' is after the maximum '{SpecMaximumDateTime}' supported in S7 date time representation.");

        byte MapYear(int year) => (byte)(year < 2000 ? year - 1900 : year - 2000);

        int DayOfWeekToInt(DayOfWeek dayOfWeek) => (int)dayOfWeek + 1;

        return
        [
            EncodeBcd(MapYear(dateTime.Year)),
            EncodeBcd(dateTime.Month),
            EncodeBcd(dateTime.Day),
            EncodeBcd(dateTime.Hour),
            EncodeBcd(dateTime.Minute),
            EncodeBcd(dateTime.Second),
            EncodeBcd(dateTime.Millisecond / 10),
            (byte) (dateTime.Millisecond % 10 << 4 | DayOfWeekToInt(dateTime.DayOfWeek))
        ];
    }

    /// <summary>
    /// Converts an array of <see cref="System.DateTime"/> values to a byte array.
    /// </summary>
    /// <param name="dateTimes">The DateTime values to convert.</param>
    /// <returns>A byte array containing the S7 date time representations of <paramref name="dateTimes"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any value of
    ///   <paramref name="dateTimes"/> is before <see cref="SpecMinimumDateTime"/>
    ///   or after <see cref="SpecMaximumDateTime"/>.</exception>
    public static byte[] ToByteArray(System.DateTime[] dateTimes)
    {
        var bytes = new List<byte>(dateTimes.Length * 8);
        foreach (var dateTime in dateTimes) bytes.AddRange(ToByteArray(dateTime));

        return bytes.ToArray();
    }

    private static System.DateTime FromByteArrayImpl(IList<byte>? bytes)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Count != 8)
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Count,
                $"Parsing a DateTime requires exactly 8 bytes of input data, input data is {bytes.Count} bytes long.");

        int DecodeBcd(byte input) => 10 * (input >> 4) + (input & 0b00001111);

        int ByteToYear(byte bcdYear)
        {
            var input = DecodeBcd(bcdYear);
            if (input < 90) return input + 2000;
            if (input < 100) return input + 1900;

            throw new ArgumentOutOfRangeException(nameof(bcdYear), bcdYear,
                $"Value '{input}' is higher than the maximum '99' of S7 date and time representation.");
        }

        int AssertRangeInclusive(int input, byte min, byte max, string field)
        {
            if (input < min)
                throw new ArgumentOutOfRangeException(nameof(input), input,
                    $"Value '{input}' is lower than the minimum '{min}' allowed for {field}.");
            if (input > max)
                throw new ArgumentOutOfRangeException(nameof(input), input,
                    $"Value '{input}' is higher than the maximum '{max}' allowed for {field}.");

            return input;
        }

        var year = ByteToYear(bytes[0]);
        var month = AssertRangeInclusive(DecodeBcd(bytes[1]), 1, 12, "month");
        var day = AssertRangeInclusive(DecodeBcd(bytes[2]), 1, 31, "day of month");
        var hour = AssertRangeInclusive(DecodeBcd(bytes[3]), 0, 23, "hour");
        var minute = AssertRangeInclusive(DecodeBcd(bytes[4]), 0, 59, "minute");
        var second = AssertRangeInclusive(DecodeBcd(bytes[5]), 0, 59, "second");
        var hsec = AssertRangeInclusive(DecodeBcd(bytes[6]), 0, 99, "first two millisecond digits");
        var msec = AssertRangeInclusive(bytes[7] >> 4, 0, 9, "third millisecond digit");
        _ = AssertRangeInclusive(bytes[7] & 0b00001111, 1, 7, "day of week");

        return new System.DateTime(year, month, day, hour, minute, second, hsec * 10 + msec);
    }
}

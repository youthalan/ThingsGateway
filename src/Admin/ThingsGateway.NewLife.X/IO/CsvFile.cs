﻿using System.Text;

using ThingsGateway.NewLife.Collections;

namespace ThingsGateway.NewLife.IO;

/// <summary>Csv文件</summary>
/// <remarks>
/// 文档 https://newlifex.com/core/csv_file
/// 支持整体读写以及增量式读写，目标是读写超大Csv文件
/// </remarks>
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
public class CsvFile : IDisposable, IAsyncDisposable
#else
public class CsvFile : IDisposable
#endif
{
    #region 属性
    /// <summary>文件编码</summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    private readonly Stream _stream;
    private readonly Boolean _leaveOpen;

    /// <summary>分隔符。默认逗号</summary>
    public Char Separator { get; set; } = ',';
    #endregion

    #region 构造
    /// <summary>数据流实例化</summary>
    /// <param name="stream"></param>
    public CsvFile(Stream stream) => _stream = stream;

    /// <summary>数据流实例化</summary>
    /// <param name="stream"></param>
    /// <param name="leaveOpen">保留打开</param>
    public CsvFile(Stream stream, Boolean leaveOpen)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    /// <summary>Csv文件实例化</summary>
    /// <param name="file"></param>
    /// <param name="write"></param>
    public CsvFile(String file, Boolean write = false)
    {
        file = file.GetFullPath();
        if (write)
            _stream = new FileStream(file.EnsureDirectory(true), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        else
            _stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }

    private Boolean _disposed;
    /// <summary>销毁</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(Boolean disposing)
    {
        if (_disposed) return;
        _disposed = true;

        // 必须刷新写入器，否则可能丢失一截数据
        _writer?.Flush();

        if (!_leaveOpen && _stream != null)
        {
            _reader.TryDispose();

            _writer.TryDispose();

            _stream.Close();
        }
    }

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    /// <summary>异步销毁</summary>
    /// <returns></returns>
    public virtual async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // 必须刷新写入器，否则可能丢失一截数据
        if (_writer != null) await _writer.FlushAsync().ConfigureAwait(false);

        if (!_leaveOpen && _stream != null)
        {
            _reader.TryDispose();

            if (_writer != null) await _writer.DisposeAsync().ConfigureAwait(false);

            await _stream.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
#endif
    #endregion

    #region 读取
    private Int32 _columnCount;
    /// <summary>读取一行</summary>
    /// <returns></returns>
    public String[]? ReadLine()
    {
        EnsureReader();

        var line = _reader?.ReadLine();
        if (line == null) return null;

        var list = new List<String>();

        // 直接分解，引号合并
        var arr = line.Split(Separator);
        // 如果字段数不足，可能有换行符，读取后面的行
        while (_columnCount > 0 && arr.Length < _columnCount)
        {
            var next = _reader?.ReadLine();
            if (next == null) break;

            line += Environment.NewLine + next;

            arr = line.Split(Separator);
        }
        for (var i = 0; i < arr.Length; i++)
        {
            var txt = (arr[i] + "").Trim();
            if (txt.Length >= 2 && txt[0] == '\"' && txt[^1] == '\"')
            {
                txt = txt[1..^1];

                // 两个引号是一个引号的转义
                txt = txt.Replace("\"\"", "\"");
            }

            list.Add(txt);
        }

        // 记录列数
        if (_columnCount == 0 && list.Count > 0) _columnCount = list.Count;

        return list.ToArray();
    }

    /// <summary>读取所有行</summary>
    /// <returns></returns>
    public IEnumerable<String[]> ReadAll()
    {
        while (true)
        {
            var line = ReadLine();
            if (line == null) break;

            yield return line;
        }
    }

    private StreamReader? _reader;
    private void EnsureReader()
    {
        _reader ??= new StreamReader(_stream, Encoding);
    }
    #endregion

    #region 写入
    /// <summary>写入全部</summary>
    /// <param name="data"></param>
    public void WriteAll(IEnumerable<IEnumerable<Object?>> data)
    {
        foreach (var line in data)
        {
            WriteLine(line);
        }
    }

    /// <summary>写入一行</summary>
    /// <param name="line"></param>
    public void WriteLine(IEnumerable<Object?> line)
    {
        EnsureWriter();

        if (_writer == null) throw new ArgumentNullException(nameof(_writer));

        var str = BuildLine(line);

        _writer.WriteLine(str);
    }

    /// <summary>
    /// 写入一行
    /// </summary>
    /// <param name="values"></param>
    public void WriteLine(params Object[] values) => WriteLine(line: values);

    /// <summary>异步写入一行</summary>
    /// <param name="line"></param>
    public async Task WriteLineAsync(IEnumerable<Object> line)
    {
        EnsureWriter();

        if (_writer == null) throw new ArgumentNullException(nameof(_writer));

        var str = BuildLine(line);

        await _writer.WriteLineAsync(str).ConfigureAwait(false);
    }

    /// <summary>构建一行</summary>
    /// <param name="line"></param>
    /// <returns></returns>
    protected virtual String BuildLine(IEnumerable<Object?> line)
    {
        var sb = Pool.StringBuilder.Get();

        foreach (var item in line)
        {
            if (sb.Length > 0) sb.Append(Separator);

            if (item is DateTime dt)
            {
                sb.Append(dt.ToFullString(""));
            }
            else if (item is Boolean b)
            {
                sb.Append(b ? "1" : "0");
            }
            else
            {
                if (item is not String str) str = item + "";

                // 避免出现科学计数问题 数据前增加制表符"\t"
                // 不同软件显示不太一样 wps超过9位就自动转为科学计数，有的软件是超过11位，所以采用最小范围9
                if (str.Length > 9 && Int64.TryParse(str, out _))
                {
                    sb.Append('\t');
                    sb.Append(str);
                }
                else if (str.Contains('"'))
                {
                    sb.Append('\"');
                    sb.Append(str.Replace("\"", "\"\""));
                    sb.Append('\"');
                }
                else if (str.Contains(Separator) || str.Contains('\r') || str.Contains('\n'))
                {
                    sb.Append('\"');
                    sb.Append(str);
                    sb.Append('\"');
                }
                else
                    sb.Append(str);
            }
        }

        return sb.Return(true);
    }

    private StreamWriter? _writer;
    private void EnsureWriter()
    {
        _writer ??= new StreamWriter(_stream, Encoding, 1024, _leaveOpen);
    }
    #endregion
}
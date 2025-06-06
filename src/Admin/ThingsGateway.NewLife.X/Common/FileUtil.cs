﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Text;

namespace ThingsGateway.NewLife;

/// <summary>
/// FileUtil
/// </summary>
public class FileUtil
{
    /// <summary>
    /// 读取文件
    /// </summary>
    public static string ReadFile(string path, Encoding? encoding = default)
    {
        encoding ??= Encoding.UTF8;
        if (!File.Exists(path))
        {
            return null;
        }

        StreamReader streamReader = new StreamReader(path, encoding);
        string result = streamReader.ReadToEnd();
        streamReader.Close();
        streamReader.Dispose();
        return result;
    }

    public static void DeleteFile(string file)
    {
        if (File.Exists(file))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
    }

    public static void WriteFile(string path, string data)
    {
        File.WriteAllText(path, data);
    }

}

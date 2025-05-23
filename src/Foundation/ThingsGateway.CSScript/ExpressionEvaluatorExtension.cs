﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using CSScripting;

using CSScriptLib;

using System.Text;

using ThingsGateway.NewLife;
using ThingsGateway.NewLife.Caching;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application.Extensions;

/// <summary>
/// 读写表达式脚本
/// </summary>
public interface ReadWriteExpressions
{
    public TouchSocket.Core.ILog? Logger { get; set; }

    /// <summary>
    /// 获取新值
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    object GetNewValue(object a);
}

/// <summary>
/// 表达式扩展
/// </summary>
public static class ExpressionEvaluatorExtension
{
    private static string CacheKey = $"{nameof(ExpressionEvaluatorExtension)}-{nameof(GetReadWriteExpressions)}";

    private static object m_waiterLock = new object();

    static ExpressionEvaluatorExtension()
    {
        var temp = Environment.GetEnvironmentVariable("CSS_CUSTOM_TEMPDIR");
        if (temp.IsNullOrWhiteSpace())
        {
            var tempDir = Path.Combine(AppContext.BaseDirectory, "CSSCRIPT");
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {

                }
            }

            Directory.CreateDirectory(tempDir);//重新创建，防止缓存的一些目录信息错误
            Environment.SetEnvironmentVariable("CSS_CUSTOM_TEMPDIR", tempDir); //传入变量
        }

        Instance.KeyExpired += Instance_KeyExpired;
    }

    private static void Instance_KeyExpired(object sender, KeyEventArgs e)
    {
        try
        {
            if (Instance.GetAll().TryGetValue(e.Key, out var item))
            {
                item?.Value?.TryDispose();
                item?.Value?.GetType().Assembly.Unload();
            }
        }
        catch
        {
        }
    }

    private static MemoryCache Instance { get; set; } = new MemoryCache();

    /// <summary>
    /// 添加或获取脚本，非线程安全
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static ReadWriteExpressions GetOrAddScript(string source)
    {
        var field = $"{CacheKey}-{source}";
        var runScript = Instance.Get<ReadWriteExpressions>(field);
        if (runScript == null)
        {
            if (!source.Contains("return"))
            {
                source = $"return {source}";//只判断简单脚本中可省略return字符串
            }
            var src = source.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var _using = new StringBuilder();
            var _body = new StringBuilder();
            src.ToList().ForEach(l =>
            {
                if (l.StartsWith("using "))
                {
                    _using.AppendLine(l);
                }
                else
                {
                    _body.AppendLine(l);
                }
            });
            // 动态加载并执行代码
            runScript = CSScript.Evaluator.With(eval => eval.IsAssemblyUnloadingEnabled = true).LoadCode<ReadWriteExpressions>(
                $@"
        using System;
        using System.Linq;
        using System.Collections.Generic;
        using Newtonsoft.Json;
        using Newtonsoft.Json.Linq;
        using ThingsGateway.Gateway.Application;
        using ThingsGateway.NewLife;
        using ThingsGateway.NewLife.Extension;
        using ThingsGateway.NewLife.Json.Extension;
        using ThingsGateway.Gateway.Application.Extensions;
        {_using}
        public class Script:ReadWriteExpressions
        {{
            public TouchSocket.Core.ILog? Logger {{ get; set; }}
            public object GetNewValue(object raw)
            {{
                   {_body};
            }}
        }}
    ");
            Instance.Set(field, runScript);
        }

        return runScript;
    }

    /// <summary>
    /// 计算表达式：例如：(int)raw*100，raw为原始值
    /// </summary>
    public static object GetExpressionsResult(this string expressions, object rawvalue)
    {
        if (string.IsNullOrWhiteSpace(expressions))
        {
            return rawvalue;
        }
        var readWriteExpressions = GetReadWriteExpressions(expressions);
        var value = readWriteExpressions.GetNewValue(rawvalue);
        return value;
    }

    /// <summary>
    /// 计算表达式：例如：(int)raw*100，raw为原始值
    /// </summary>
    public static object GetExpressionsResult(this string expressions, object rawvalue, ILog logger)
    {
        if (string.IsNullOrWhiteSpace(expressions))
        {
            return rawvalue;
        }
        var readWriteExpressions = GetReadWriteExpressions(expressions);
        readWriteExpressions.Logger = logger;
        var value = readWriteExpressions.GetNewValue(rawvalue);
        return value;
    }

    /// <summary>
    /// 执行脚本获取返回值ReadWriteExpressions
    /// </summary>
    public static ReadWriteExpressions GetReadWriteExpressions(string source)
    {
        var field = $"{CacheKey}-{source}";
        var runScript = Instance.Get<ReadWriteExpressions>(field);
        if (runScript == null)
        {
            lock (m_waiterLock)
            {
                runScript = GetOrAddScript(source);
            }
        }
        Instance.SetExpire(field, TimeSpan.FromHours(1));

        return runScript;
    }
    public static void SetExpire(string source, TimeSpan? timeSpan = null)
    {
        var field = $"{CacheKey}-{source}";
        Instance.SetExpire(field, timeSpan ?? TimeSpan.FromHours(1));
    }
}

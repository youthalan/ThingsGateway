﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using SqlSugar;

using ThingsGateway.Extension;
using ThingsGateway.NewLife.Log;

namespace ThingsGateway.Admin.Application;

/// <summary>
/// 数据库上下文对象
/// </summary>
public static class DbContext
{
    /// <summary>
    /// SqlSugar 数据库实例
    /// </summary>
    public static readonly SqlSugarScope Db;

    /// <summary>
    /// 读取配置文件中的 ConnectionStrings:Sqlsugar 配置节点
    /// </summary>
    public static readonly SqlSugarOptions DbConfigs;

    /// <summary>
    /// 获取数据库连接
    /// </summary>
    /// <returns></returns>
    public static SqlSugarClient GetDB<T>()
    {
        return Db.GetConnectionScopeWithAttr<T>().CopyNew();
    }

    private static ISugarAopService sugarAopService;
    private static ISugarAopService SugarAopService
    {
        get
        {
            if (sugarAopService == null)
            {
                sugarAopService = App.RootServices.GetService<ISugarAopService>();
            }
            return sugarAopService;
        }
    }

    static DbContext()
    {
        // 配置映射
        DbConfigs = App.GetOptions<SqlSugarOptions>();
        DbConfigs = App.RootServices.GetService<ISugarConfigAopService>().Config(DbConfigs);
        Db = new(DbConfigs.Select(a => (ConnectionConfig)a).ToList(), db =>
        {
            DbConfigs.ForEach(it =>
            {
                var sqlsugarScope = db.GetConnectionScope(it.ConfigId);//获取当前库
                MoreSetting(sqlsugarScope);//更多设置
                SugarAopService.AopSetting(sqlsugarScope, it.IsShowSql);//aop配置
            }
            );
        });
    }


    /// <summary>
    /// 实体更多配置
    /// </summary>
    /// <param name="db"></param>
    private static void MoreSetting(SqlSugarScopeProvider db)
    {
        db.CurrentConnectionConfig.MoreSettings = new ConnMoreSettings
        {
            SqlServerCodeFirstNvarchar = true//设置默认nvarchar
        };
    }

    /// <inheritdoc/>
    public static void WriteErrorLogWithSql(string msg)
    {
        XTrace.Log.Error("【Sql执行错误时间】：" + DateTime.Now.ToDefaultDateTimeFormat());
        XTrace.Log.Error("【Sql语句】：" + msg + Environment.NewLine);
    }

    /// <inheritdoc/>
    public static void WriteLog(string msg)
    {
        Console.WriteLine("【库操作】：" + msg + Environment.NewLine);
    }

    /// <inheritdoc/>
    public static void WriteLogWithSql(string msg)
    {
        Console.WriteLine("【Sql执行时间】：" + DateTime.Now.ToDefaultDateTimeFormat());
        Console.WriteLine("【Sql语句】：" + msg + Environment.NewLine);
    }



    public static async Task BulkCopyAsync<TITEM>(this SqlSugarClient db, List<TITEM> datas, int size) where TITEM : class, new()
    {
        switch (db.CurrentConnectionConfig.DbType)
        {
            case DbType.MySql:
            case DbType.SqlServer:
            case DbType.Sqlite:
            case DbType.Oracle:
            case DbType.PostgreSQL:
            case DbType.Dm:
            case DbType.MySqlConnector:
            case DbType.Kdbndp:
                await db.Fastest<TITEM>().PageSize(size).BulkCopyAsync(datas).ConfigureAwait(false);
                break;
            default:
                await db.Insertable(datas).PageSize(size).ExecuteCommandAsync().ConfigureAwait(false);
                break;
        }

    }
    public static async Task BulkUpdateAsync<TITEM>(this SqlSugarClient db, List<TITEM> datas, int size) where TITEM : class, new()
    {
        switch (db.CurrentConnectionConfig.DbType)
        {
            case DbType.MySql:
            case DbType.SqlServer:
            case DbType.Sqlite:
            case DbType.Oracle:
            case DbType.PostgreSQL:
            case DbType.Dm:
            case DbType.MySqlConnector:
            case DbType.Kdbndp:
                await db.Fastest<TITEM>().PageSize(size).BulkUpdateAsync(datas).ConfigureAwait(false);
                break;
            default:
                await db.Updateable(datas).PageSize(size).ExecuteCommandAsync().ConfigureAwait(false);
                break;
        }
    }
}

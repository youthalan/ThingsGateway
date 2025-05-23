﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using BootstrapBlazor.Components;

using System.ComponentModel.DataAnnotations;

namespace ThingsGateway.Plugin.SqlDB;
public enum DbType
{
    MySql = 0,
    SqlServer = 1,
    Sqlite = 2,
    Oracle = 3,
    PostgreSQL = 4,
}
public enum SqlDBSplitType
{
    Day = 0,
    Week = 1,
    Month = 2,
    Month_6 = 1000,
    Season = 3,
    Year = 4,
}
/// <summary>
/// SqlDBProducerProperty
/// </summary>
public class SqlDBProducerProperty : BusinessPropertyWithCacheInterval
{
    [DynamicProperty]
    public bool IsReadDB { get; set; } = false;

    [DynamicProperty]
    public bool IsHistoryDB { get; set; } = true;

    [DynamicProperty]
    public string ReadDBTableName { get; set; } = "ReadDBTableName";

    [DynamicProperty]
    public string HistoryDBTableName { get; set; } = "HistoryDBTableName";

    [DynamicProperty]
    public DbType DbType { get; set; } = DbType.SqlServer;

    [DynamicProperty]
    public SqlDBSplitType SqlDBSplitType { get; set; } = SqlDBSplitType.Week;

    [DynamicProperty]
    [Required]
    [AutoGenerateColumn(ComponentType = typeof(Textarea), Rows = 1)]
    public string BigTextConnectStr { get; set; } = "server=.;uid=sa;pwd=111111;database=test;";


    /// <summary>
    /// 实时表间隔上传时间
    /// </summary>
    [DynamicProperty]
    public virtual string RealTableBusinessInterval { get; set; } = "3000";

    /// <summary>
    /// 实时表脚本
    /// </summary>
    [DynamicProperty(Remark = "必须为间隔上传，才生效")]
    [AutoGenerateColumn(Visible = true, IsVisibleWhenEdit = false, IsVisibleWhenAdd = false)]
    public string? BigTextScriptRealTable { get; set; }
    /// <summary>
    /// 历史表脚本
    /// </summary>
    [DynamicProperty(Remark = "必须为间隔上传，才生效")]
    [AutoGenerateColumn(Visible = true, IsVisibleWhenEdit = false, IsVisibleWhenAdd = false)]
    public string? BigTextScriptHistoryTable { get; set; }

}

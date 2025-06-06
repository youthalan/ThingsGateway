﻿// ------------------------------------------------------------------------------
// 此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
// 此代码版权（除特别声明外的代码）归作者本人Diego所有
// 源代码使用协议遵循本仓库的开源协议及附加协议
// Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
// Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
// 使用文档：https://thingsgateway.cn/
// QQ群：605534569
// ------------------------------------------------------------------------------

using BootstrapBlazor.Components;

using SqlSugar;

using System.ComponentModel.DataAnnotations;

namespace ThingsGateway.Admin.Application;

/// <summary>
/// 职位表
///</summary>
[SugarTable("sys_position", TableDescription = "职位表")]
[Tenant(SqlSugarConst.DB_Admin)]
public class SysPosition : BaseEntity
{
    /// <summary>
    /// 组织id
    ///</summary>
    [SugarColumn(ColumnName = "OrgId", ColumnDescription = "组织id")]
    [AutoGenerateColumn(Ignore = true)]
    [MinValue(1)]
    public virtual long OrgId { get; set; }

    /// <summary>
    /// 名称
    ///</summary>
    [SugarColumn(ColumnName = "Name", ColumnDescription = "名称", Length = 200)]
    [AutoGenerateColumn(Visible = true, Sortable = true, Filterable = true)]
    [Required]
    public virtual string Name { get; set; }

    /// <summary>
    /// 编码
    ///</summary>
    [SugarColumn(ColumnName = "Code", ColumnDescription = "编码", Length = 200)]
    [AutoGenerateColumn(Visible = true, Sortable = true, Filterable = true)]
    public string Code { get; set; }


    [SugarColumn(ColumnName = "Status", ColumnDescription = "启用")]
    [AutoGenerateColumn(Visible = true, Sortable = true, Filterable = true)]
    public bool Status { get; set; } = true;

    /// <summary>
    /// 分类
    ///</summary>
    [SugarColumn(ColumnName = "Category", ColumnDescription = "分类")]
    [AutoGenerateColumn(Visible = true, Sortable = true, Filterable = true)]
    public virtual PositionCategoryEnum Category { get; set; }

}

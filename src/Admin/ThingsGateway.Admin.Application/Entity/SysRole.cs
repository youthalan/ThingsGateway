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

using SqlSugar;

using System.ComponentModel.DataAnnotations;

namespace ThingsGateway.Admin.Application;

/// <summary>
/// 系统角色表
///</summary>
[SugarTable("sys_role", TableDescription = "系统角色表")]
[Tenant(SqlSugarConst.DB_Admin)]
public class SysRole : BaseEntity
{
    /// <summary>
    /// 名称
    ///</summary>
    [SugarColumn(ColumnDescription = "名称", Length = 200)]
    [Required]
    [AutoGenerateColumn(Visible = true, Sortable = true, Filterable = true)]
    public virtual string Name { get; set; }

    /// <summary>
    /// 编码
    ///</summary>
    [SugarColumn(ColumnDescription = "编码", Length = 200)]
    [AutoGenerateColumn(Visible = true, Sortable = true, Filterable = true)]
    public string Code { get; set; }

    /// <summary>
    /// 分类
    ///</summary>
    [SugarColumn(ColumnDescription = "分类", IsNullable = false)]
    [AutoGenerateColumn(Visible = true, Sortable = true, Filterable = true)]
    public virtual RoleCategoryEnum Category { get; set; }

    /// <summary>
    /// 组织id
    ///</summary>
    [SugarColumn(ColumnName = "OrgId", ColumnDescription = "组织id", IsNullable = false)]
    [AutoGenerateColumn(Ignore = true)]
    public long OrgId { get; set; }

    /// <summary>
    /// 默认数据范围
    ///</summary>
    [SugarColumn(ColumnName = "DefaultDataScope", ColumnDescription = "默认数据范围", IsJson = true, ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false)]
    [AutoGenerateColumn(Ignore = true)]
    public virtual DefaultDataScope DefaultDataScope { get; set; } = new();

    public override bool Equals(object? obj)
    {
        if (obj == null || !(obj is SysRole))
        {
            return false;
        }

        return Id == ((SysRole)obj).Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}



/// <summary>
/// 默认数据范围
/// </summary>
public class DefaultDataScope
{
    /// <summary>
    /// 数据范围
    /// </summary>
    public DataScopeEnum ScopeCategory { get; set; }

    /// <summary>
    /// 自定义机构范围列表
    /// </summary>
    public List<long> ScopeDefineOrgIdList { get; set; } = new List<long>();
}

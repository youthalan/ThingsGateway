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

using ThingsGateway.Extension.Generic;

namespace ThingsGateway.Plugin.DB;

public class SqlDBPageInput : ITableSearchModel
{
    /// <summary>
    /// 时间区间
    /// </summary>
    public DateTimeRangeValue? SearchDate { get; set; }

    /// <summary>
    /// 变量名称
    /// </summary>
    public virtual string? VariableName { get; set; }

    /// <summary>
    /// 设备名称
    /// </summary>
    public virtual string? DeviceName { get; set; }
    /// <inheritdoc/>
    public IEnumerable<IFilterAction> GetSearches()
    {
        var ret = new List<IFilterAction>();
        ret.AddIF(!string.IsNullOrEmpty(VariableName), () => new SearchFilterAction(nameof(IDBHistoryValue.Name), VariableName));
        ret.AddIF(!string.IsNullOrEmpty(DeviceName), () => new SearchFilterAction(nameof(IDBHistoryValue.DeviceName), DeviceName));
        ret.AddIF(SearchDate != null, () => new SearchFilterAction(nameof(IDBHistoryValue.CreateTime), SearchDate!.Start, FilterAction.GreaterThanOrEqual));
        ret.AddIF(SearchDate != null, () => new SearchFilterAction(nameof(IDBHistoryValue.CreateTime), SearchDate!.End, FilterAction.LessThanOrEqual));
        return ret;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        SearchDate = null;
        VariableName = null;
        DeviceName = null;
    }
}

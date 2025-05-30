﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using ThingsGateway.Admin.Application;

namespace ThingsGateway.Admin.Razor;

public partial class SysOrgEdit
{
    [Parameter]
    [NotNull]
    public SysOrg? Model { get; set; }

    [NotNull]
    private List<TreeViewItem<long>> Items { get; set; }

    [Inject]
    [NotNull]
    private ISysOrgService SysOrgService { get; set; }

    private List<SelectedItem> BoolItems;

    protected override async Task OnInitializedAsync()
    {
        BoolItems = LocalizerUtil.GetBoolItems(Model.GetType(), nameof(Model.Status));
        var items = (await SysOrgService.SelectorAsync());
        Items = OrgUtil.BuildTreeIdItemList(items, new List<long> { Model.ParentId });
        if (!AppContext.CurrentUser.IsGlobal)
            Model.Category = OrgEnum.DEPT;
        await base.OnInitializedAsync();
    }
    private long Key { get; set; }
    [Inject]
    private BlazorAppContext AppContext { get; set; }
    private Task CleanParentId()
    {
        if (!AppContext.CurrentUser.IsGlobal)
            Model.ParentId = Items.FirstOrDefault()?.Value ?? 0;
        else
            Model.ParentId = 0;
        Key = CommonUtils.GetSingleId();
        return Task.CompletedTask;
    }
}

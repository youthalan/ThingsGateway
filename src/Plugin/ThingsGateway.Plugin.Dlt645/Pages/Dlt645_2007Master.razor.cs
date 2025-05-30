﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

#pragma warning disable CA2007 // 考虑对等待的任务调用 ConfigureAwait
using BootstrapBlazor.Components;

using Microsoft.AspNetCore.Components;

using ThingsGateway.Foundation;
using ThingsGateway.Foundation.Dlt645;

using TouchSocket.Core;

namespace ThingsGateway.Debug;

public partial class Dlt645_2007Master : ComponentBase, IDisposable
{
    private ThingsGateway.Foundation.Dlt645.Dlt645_2007Master _plc = new();

    private string LogPath;

    ~Dlt645_2007Master()
    {
        this.SafeDispose();
    }

    private DeviceComponent DeviceComponent { get; set; }

    public void Dispose()
    {
        _plc?.SafeDispose();
        GC.SuppressFinalize(this);
    }

    private void OnConfimClick((IChannel channel, string logPath) value)
    {
        _plc.InitChannel(value.channel);
        LogPath = value.logPath;
    }

    private async Task OnShowAddressUI(string address)
    {
        var op = new DialogOption()
        {
            IsScrolling = false,
            ShowMaximizeButton = true,
            Size = Size.ExtraLarge,
            Title = nameof(Dlt645_2007Address),
            ShowFooter = false,
            ShowCloseButton = false,
        };

        op.Component = BootstrapDynamicComponent.CreateComponent<Dlt645_2007AddressComponent>(new Dictionary<string, object?>
        {
             {nameof(Dlt645_2007AddressComponent.ModelChanged),  (string a) =>
            {
                if(DeviceComponent!=null)
                {
                    DeviceComponent.SetRegisterAddress(a);
                }
            }},
            {nameof(Dlt645_2007AddressComponent.Model),address },
        });

        await DialogService.Show(op);
    }

    [Inject]
    DialogService DialogService { get; set; }
}

﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://diego2098.gitee.io/thingsgateway-docs/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.AspNetCore.SignalR;

namespace ThingsGateway.Admin.Application;

/// <summary>
/// <inheritdoc cref="INoticeService"/>
/// </summary>
public class SignalrNoticeService : INoticeService
{
    /// <inheritdoc/>
    public async Task NewMesage(IEnumerable<string>? clientIds, AppMessage message)
    {
        //发送消息给用户
        await GetHubContext().Clients.Users(clientIds).NewMesage(message).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UserLoginOut(IEnumerable<string>? clientIds, string message)
    {
        //发送消息给用户
        await GetHubContext().Clients.Users(clientIds).LoginOut(message).ConfigureAwait(false);
    }

    public async Task NavigationMesage(IEnumerable<string>? clientIds, string uri, string message)
    {
        await GetHubContext().Clients.Users(clientIds).NavigationMesage(uri, message).ConfigureAwait(false);
    }
    #region MyRegion

    /// <summary>
    /// 获取hubContext
    /// </summary>
    /// <returns></returns>
    private static IHubContext<SysHub, ISysHub> GetHubContext()
    {
        //解析服务
        var service = App.GetService<IHubContext<SysHub, ISysHub>>();
        return service;
    }

    #endregion MyRegion
}
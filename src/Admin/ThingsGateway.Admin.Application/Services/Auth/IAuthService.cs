﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

namespace ThingsGateway.Admin.Application;

/// <summary>
/// 定义身份验证服务的接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="input">登录参数</param>
    /// <param name="isCookie">是否使用 cookie 登录方式</param>
    /// <returns>登录输出</returns>
    Task<LoginOutput> LoginAsync(LoginInput input, bool isCookie = true);

    /// <summary>
    /// 注销当前用户
    /// </summary>
    Task LoginOutAsync();
}

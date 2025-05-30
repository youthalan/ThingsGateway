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

using System.Text.RegularExpressions;

using ThingsGateway.DataEncryption;
using ThingsGateway.Extension;
using ThingsGateway.Extension.Generic;
using ThingsGateway.FriendlyException;
using ThingsGateway.NewLife.Json.Extension;
using ThingsGateway.Razor;

namespace ThingsGateway.Admin.Application;

internal sealed class UserCenterService : BaseService<SysUser>, IUserCenterService
{
    private readonly ISysDictService _configService;
    private readonly IRelationService _relationService;
    private readonly ISysResourceService _sysResourceService;
    private readonly ISysUserService _userService;
    private readonly IVerificatInfoService _verificatInfoService;

    public UserCenterService(ISysUserService userService,
        IRelationService relationService,
        IVerificatInfoService verificatInfoService,

        ISysResourceService sysResourceService,
        ISysDictService configService)
    {
        _userService = userService;
        _relationService = relationService;
        _sysResourceService = sysResourceService;
        _configService = configService;
        _verificatInfoService = verificatInfoService;
    }

    #region 查询

    /// <summary>
    /// 获取个人工作台
    /// </summary>
    /// <param name="userId">用户id</param>
    public async Task<WorkbenchInfo> GetLoginWorkbenchAsync(long userId)
    {
        AppConfig? appConfig = null;
        WorkbenchInfo relationUserWorkBench = new();
        relationUserWorkBench.Id = userId;
        {
            //获取个人工作台信息
            var sysRelations = await _relationService.GetRelationByCategoryAsync(RelationCategoryEnum.UserWorkbenchData).ConfigureAwait(false);
            var sysRelation = sysRelations.FirstOrDefault(it => it.ObjectId == userId);//获取个人工作台
            if (sysRelation != null)
            {
                //如果有数据直接返回个人工作台
                relationUserWorkBench.Shortcuts = sysRelation.ExtJson!.ToLower().FromJsonNetString<List<long>>();
            }
            else
            {
                //如果没数据去系统配置里取默认的工作台
                appConfig = await _configService.GetAppConfigAsync().ConfigureAwait(false);
                relationUserWorkBench.Shortcuts = appConfig.PagePolicy.Shortcuts;//返回工作台信息
            }
        }
        return relationUserWorkBench;
    }

    /// <summary>
    /// 获取菜单列表，不会转成树形数据
    /// </summary>
    /// <param name="userId">用户id</param>
    /// <param name="moduleId">模块id</param>
    /// <returns>菜单列表</returns>
    public async Task<IEnumerable<SysResource>> GetOwnMenuAsync(long userId, long moduleId)
    {
        var result = new List<SysResource>();
        //获取用户信息
        var userInfo = await _userService.GetUserByIdAsync(userId).ConfigureAwait(false);
        if (userInfo != null)
        {
            //获取用户所拥有的资源集合
            var resourceList = await _relationService.GetRelationListByObjectIdAndCategoryAsync(userInfo.Id, RelationCategoryEnum.UserHasResource).ConfigureAwait(false);
            if (!resourceList.Any())//如果没有就获取角色的
                //获取角色所拥有的资源集合
                resourceList = await _relationService.GetRelationListByObjectIdListAndCategoryAsync(userInfo.RoleIdList!,
                    RelationCategoryEnum.RoleHasResource).ConfigureAwait(false);

            var all = await _sysResourceService.GetAllAsync().ConfigureAwait(false);

            //定义菜单ID列表
            var menuIdList = resourceList.Select(r => r.TargetId.ToLong());

            //获取所有的菜单 ，并按分类和排序码排序 //首页例外
            var allMenuList = (all).Where(a => a.Category == ResourceCategoryEnum.Menu
           ).WhereIf(moduleId > 0, a => a.Module == moduleId).OrderBy(a => a.Module).ThenBy(a => a.SortCode);

            //输出的用户权限菜单
            IEnumerable<SysResource> myMenus;

            //管理员拥有全部权限
            if (UserManager.SuperAdmin)
            {
                myMenus = allMenuList;
            }
            else
            {
                //获取我的菜单列表
                myMenus = allMenuList.Where(it => menuIdList.Contains(it.Id));
            }

            // 对获取到的角色对应的菜单列表进行处理，获取父列表
            var parentList = _sysResourceService.GetMyParentResources(allMenuList, myMenus);

            return myMenus.Concat(parentList);
        }
        return Enumerable.Empty<SysResource>();
    }

    #endregion 查询

    #region 编辑

    /// <inheritdoc />
    [OperDesc("UpdatePassword", isRecordPar: false)]
    public async Task UpdatePasswordAsync(UpdatePasswordInput input)
    {
        var websiteOptions = App.GetOptions<WebsiteOptions>()!;
        if (websiteOptions.Demo)
        {
            throw Oops.Bah(Localizer["DemoCanotUpdatePassword"]);
        }
        if (input.NewPassword != input.ConfirmPassword)
            throw Oops.Bah(Localizer["ConfirmPasswordDiff"]);

        //获取用户信息
        var userInfo = await _userService.GetUserByIdAsync(UserManager.UserId).ConfigureAwait(false);
        var password = input.Password;
        if (userInfo!.Password != password)
            throw Oops.Bah(Localizer["OldPasswordError"]);

        var passwordPolicy = (await _configService.GetAppConfigAsync().ConfigureAwait(false)).PasswordPolicy;
        if (passwordPolicy.PasswordMinLen > input.NewPassword.Length)
            throw Oops.Bah(Localizer["PasswordLengthLess", passwordPolicy.PasswordMinLen]);
        if (passwordPolicy.PasswordContainNum && !Regex.IsMatch(input.NewPassword, "[0-9]"))
            throw Oops.Bah(Localizer["PasswordMustNum"]);
        if (passwordPolicy.PasswordContainLower && !Regex.IsMatch(input.NewPassword, "[a-z]"))
            throw Oops.Bah(Localizer["PasswordMustLow"]);
        if (passwordPolicy.PasswordContainUpper && !Regex.IsMatch(input.NewPassword, "[A-Z]"))
            throw Oops.Bah(Localizer["PasswordMustUpp"]);
        if (passwordPolicy.PasswordContainChar && !Regex.IsMatch(input.NewPassword, "[~!@#$%^&*()_+`\\-={}|\\[\\]:\";'<>?,./]"))
            throw Oops.Bah(Localizer["PasswordMustSpecial"]);

        var newPassword = DESEncryption.Encrypt(input.NewPassword);
        using var db = GetDB();
        await db.UpdateSetColumnsTrueAsync<SysUser>(it => new SysUser() { Password = newPassword }, it => it.Id == userInfo.Id).ConfigureAwait(false);
        _userService.DeleteUserFromCache(UserManager.UserId);//cache删除用户数据

        //将这些用户踢下线，并永久注销这些用户
        var verificatInfoIds = _verificatInfoService.GetListByUserId(UserManager.UserId);
        //从列表中删除
        _verificatInfoService.Delete(verificatInfoIds.Select(a => a.Id).ToList());
        await UserLoginOut(UserManager.UserId, verificatInfoIds.SelectMany(a => a.ClientIds).ToList()).ConfigureAwait(false);
    }

    /// <inheritdoc />
    [OperDesc("UpdateUserInfo", false)]
    public async Task UpdateUserInfoAsync(SysUser input)
    {
        if (!string.IsNullOrEmpty(input.Phone))
        {
            if (!input.Phone.MatchPhoneNumber())//判断是否是手机号格式
                throw Oops.Bah(Localizer["PhoneError", input.Phone]);
        }
        if (!string.IsNullOrEmpty(input.Email))
        {
            var match = input.Email.MatchEmail();
            if (!match)
                throw Oops.Bah(Localizer["EmailError", input.Email]);
        }
        using var db = GetDB();

        //更新指定字段
        var result = await db.UpdateSetColumnsTrueAsync<SysUser>(it => new SysUser
        {
            Email = input.Email,
            Phone = input.Phone,
            Avatar = input.Avatar,
        }, it => it.Id == UserManager.UserId).ConfigureAwait(false);
        if (result)
            _userService.DeleteUserFromCache(UserManager.UserId);//cache删除用户数据
    }

    /// <inheritdoc />
    [OperDesc("WorkbenchInfo")]
    public async Task UpdateWorkbenchInfoAsync(WorkbenchInfo input)
    {
        //关系表保存个人工作台
        await _relationService.SaveRelationAsync(RelationCategoryEnum.UserWorkbenchData, input.Id, null, input.Shortcuts.ToJsonNetString(),
            true).ConfigureAwait(false);
    }

    #endregion 编辑

    #region 方法

    /// <summary>
    /// 通知用户下线
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="clientIds">Token列表</param>
    private async Task UserLoginOut(long userId, List<string> clientIds)
    {
        await NoticeUtil.UserLoginOut(new UserLoginOutEvent
        {
            Message = Localizer["PasswordEdited"],
            ClientIds = clientIds,
        }).ConfigureAwait(false);//通知用户下线
    }

    #endregion 方法
}

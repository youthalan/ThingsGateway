﻿// ------------------------------------------------------------------------
// 版权信息
// 版权归百小僧及百签科技（广东）有限公司所有。
// 所有权利保留。
// 官方网站：https://baiqian.com
//
// 许可证信息
// 项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。
// 许可证的完整文本可以在源代码树根目录中的 LICENSE-APACHE 和 LICENSE-MIT 文件中找到。
// ------------------------------------------------------------------------

namespace ThingsGateway.HttpRemote;

/// <summary>
///     HTTP 声明式提取器排序（冻结）
/// </summary>
public interface IFrozenHttpDeclarativeExtractor : IHttpDeclarativeExtractor
{
    /// <summary>
    ///     获取提取器的顺序值。值越小，提取器越晚被调用
    /// </summary>
    int Order { get; }
}
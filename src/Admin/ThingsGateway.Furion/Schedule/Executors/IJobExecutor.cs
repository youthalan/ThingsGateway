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

namespace ThingsGateway.Schedule;

/// <summary>
/// 作业处理程序执行器
/// </summary>
public interface IJobExecutor
{
    /// <summary>
    /// 执行作业处理程序
    /// </summary>
    /// <remarks>在这里可以实现超时控制，失败重试控制等等</remarks>
    /// <param name="context">作业处理程序执行前上下文</param>
    /// <param name="jobHandler">作业处理程序</param>
    /// <param name="stoppingToken">取消任务 Token</param>
    /// <returns><see cref="Task"/> 实例</returns>
    Task ExecuteAsync(JobExecutingContext context, IJob jobHandler, CancellationToken stoppingToken);
}
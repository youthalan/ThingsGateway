﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Mapster;

using System.Diagnostics;

using ThingsGateway.Foundation;
using ThingsGateway.NewLife.Threading;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.SqlHistoryAlarm;

/// <summary>
/// SqlHistoryAlarm
/// </summary>
public partial class SqlHistoryAlarm : BusinessBaseWithCacheVariableModel<HistoryAlarm>
{
    protected override ValueTask<OperResult> UpdateVarModel(IEnumerable<CacheDBItem<HistoryAlarm>> item, CancellationToken cancellationToken)
    {
        return UpdateT(item.Select(a => a.Value).OrderBy(a => a.Id), cancellationToken);
    }

    protected override ValueTask<OperResult> UpdateVarModels(IEnumerable<HistoryAlarm> item, CancellationToken cancellationToken)
    {
        return UpdateT(item, cancellationToken);
    }

    private void AlarmWorker_OnAlarmChanged(AlarmVariable alarmVariable)
    {
        if (CurrentDevice.Pause)
            return;
        AddQueueVarModel(new CacheDBItem<HistoryAlarm>(alarmVariable.Adapt<HistoryAlarm>(_config)));
    }

    private async ValueTask<OperResult> InserableAsync(List<HistoryAlarm> dbInserts, CancellationToken cancellationToken)
    {
        try
        {
            using var db = BusinessDatabaseUtil.GetDb(_driverPropertys.DbType, _driverPropertys.BigTextConnectStr);

            int result = 0;
            //.SplitTable()
            Stopwatch stopwatch = new();
            stopwatch.Start();

            if (db.CurrentConnectionConfig.DbType == SqlSugar.DbType.QuestDB)
                result = await db.Insertable(dbInserts).AS(_driverPropertys.TableName).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);//不要加分表
            else
                result = await db.Fastest<HistoryAlarm>().AS(_driverPropertys.TableName).PageSize(50000).BulkCopyAsync(dbInserts).ConfigureAwait(false);


            stopwatch.Stop();
            //var result = await db.Insertable(dbInserts).SplitTable().ExecuteCommandAsync().ConfigureAwait(false);
            if (result > 0)
            {
                CurrentDevice.SetDeviceStatus(TimerX.Now, false);
                LogMessage.Trace($"Count：{dbInserts.Count}，watchTime:  {stopwatch.ElapsedMilliseconds} ms");
            }
            return OperResult.Success;
        }
        catch (Exception ex)
        {
            CurrentDevice.SetDeviceStatus(TimerX.Now, true);
            return new OperResult(ex);
        }
    }

    private async ValueTask<OperResult> UpdateT(IEnumerable<HistoryAlarm> item, CancellationToken cancellationToken)
    {
        var result = await InserableAsync(item.ToList(), cancellationToken).ConfigureAwait(false);
        if (success != result.IsSuccess)
        {
            if (!result.IsSuccess)
                LogMessage.LogWarning(result.ToString());
            success = result.IsSuccess;
        }

        return result;
    }
}

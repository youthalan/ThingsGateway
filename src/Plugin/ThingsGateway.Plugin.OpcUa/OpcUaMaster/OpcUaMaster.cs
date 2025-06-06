﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Newtonsoft.Json.Linq;

using Opc.Ua;
using Opc.Ua.Client;

using ThingsGateway.Foundation.Extension.Generic;
using ThingsGateway.Foundation.OpcUa;
using ThingsGateway.Gateway.Application;
using ThingsGateway.NewLife;
using ThingsGateway.NewLife.Json.Extension;
using ThingsGateway.NewLife.Threading;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.OpcUa;

/// <summary>
/// <inheritdoc/>
/// </summary>
public class OpcUaMaster : CollectBase
{
    private readonly OpcUaMasterProperty _driverProperties = new();

    private ThingsGateway.Foundation.OpcUa.OpcUaMaster _plc;

    private volatile bool connectFirstFailLoged;
    private volatile bool success = true;

    /// <inheritdoc/>
    public override CollectPropertyBase CollectProperties => _driverProperties;

    /// <inheritdoc/>
    public override Type DriverDebugUIType => typeof(ThingsGateway.Debug.OpcUaMaster);

    public override Type DriverPropertyUIType => typeof(OpcUaMasterRuntimeRazor);

    public override Type DriverUIType => typeof(OpcUaMasterRuntimeRazor);

    protected override async Task InitChannelAsync(IChannel? channel, CancellationToken cancellationToken)
    {


        //载入配置
        OpcUaProperty config = new()
        {
            OpcUrl = _driverProperties.OpcUrl,
            UpdateRate = _driverProperties.UpdateRate,
            DeadBand = _driverProperties.DeadBand,
            GroupSize = _driverProperties.GroupSize,
            KeepAliveInterval = _driverProperties.KeepAliveInterval,
            UseSecurity = _driverProperties.UseSecurity,
            ActiveSubscribe = _driverProperties.ActiveSubscribe,
            UserName = _driverProperties.UserName,
            Password = _driverProperties.Password,
            CheckDomain = _driverProperties.CheckDomain,
            LoadType = _driverProperties.LoadType,
            AutoAcceptUntrustedCertificates = _driverProperties.AutoAcceptUntrustedCertificates,
        };
        if (_plc == null)
        {
            _plc = new();
            _plc.LogEvent += _plc_LogEvent;
            _plc.DataChangedHandler += DataChangedHandler;
        }
        _plc.OpcUaProperty = config;
        await base.InitChannelAsync(channel, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override bool IsConnected() => _plc?.Connected == true;

    public override string ToString()
    {
        return $"{_driverProperties.OpcUrl}";
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_plc != null)
        {
            _plc.DataChangedHandler -= DataChangedHandler;
            _plc.LogEvent -= _plc_LogEvent;

            _plc.Disconnect();
            _plc.SafeDispose();
        }

        VariableAddresDicts?.Clear();
        base.Dispose(disposing);
    }

    public override string GetAddressDescription()
    {
        return _plc?.GetAddressDescription();
    }

    private TimeTick checkTimeTick = new("60000");
    protected override async ValueTask ProtectedExecuteAsync(CancellationToken cancellationToken)
    {
        if (_plc.Session == null)
        {
            try
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                if (_plc.Session == null)
                    await _plc.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!connectFirstFailLoged)
                    LogMessage?.LogWarning(ex, "Connect Fail");

                connectFirstFailLoged = true;
                CurrentDevice.SetDeviceStatus(TimerX.Now, true, ex.Message);
                await Task.Delay(10000, cancellationToken).ConfigureAwait(false);
            }
        }
        if (_driverProperties.ActiveSubscribe)
        {

            //获取设备连接状态
            if (IsConnected())
            {


                //更新设备活动时间
                CurrentDevice.SetDeviceStatus(TimerX.Now, false);
                if (checkTimeTick.IsTickHappen())
                {

                    //如果是订阅模式，连接时添加订阅组
                    if (_plc.OpcUaProperty?.ActiveSubscribe == true && CurrentDevice.VariableSourceReads.Count > 0 && _plc.Session.SubscriptionCount < CurrentDevice.VariableSourceReads.Count)
                    {
                        try
                        {

                            foreach (var variableSourceRead in CurrentDevice.VariableSourceReads)
                            {
                                if (_plc.Session.Subscriptions.FirstOrDefault(a => a.DisplayName == variableSourceRead.RegisterAddress) == null)
                                {
                                    await _plc.AddSubscriptionAsync(variableSourceRead.RegisterAddress, variableSourceRead.VariableRuntimes.Where(a => !a.RegisterAddress.IsNullOrEmpty()).Select(a => a.RegisterAddress!).ToHashSet().ToArray(), _plc.OpcUaProperty.LoadType, cancellationToken).ConfigureAwait(false);

                                    LogMessage?.LogInformation($"AddSubscription index  {CurrentDevice.VariableSourceReads.IndexOf(variableSourceRead)}  done");

                                }
                            }
                            LogMessage?.LogInformation("AddSubscriptions done");
                        }
                        catch (Exception ex)
                        {
                            LogMessage?.LogWarning(ex, "AddSubscriptions");
                        }
                        finally
                        {
                        }
                    }
                }

            }
            else
            {
                CurrentDevice.SetDeviceStatus(TimerX.Now, true);
            }

            ScriptVariableRun(cancellationToken);
        }
        else
        {
            await base.ProtectedExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task<List<VariableSourceRead>> ProtectedLoadSourceReadAsync(List<VariableRuntime> deviceVariables)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (deviceVariables.Count > 0)
        {
            List<VariableSourceRead> variableSourceReads = new List<VariableSourceRead>();
            foreach (var deviceVariableGroups in deviceVariables.GroupBy(a => a.CollectGroup))
            {
                var dataLists = deviceVariableGroups.ChunkBetter(_driverProperties.GroupSize);

                foreach (var variable in dataLists)
                {
                    var sourVars = new VariableSourceRead()
                    {
                        TimeTick = new(_driverProperties.UpdateRate.ToString()),
                        RegisterAddress = Guid.NewGuid().ToString(),
                    };
                    foreach (var item in variable)
                    {
                        sourVars.AddVariable(item);
                    }
                    variableSourceReads.Add(sourVars);
                }

            }
            return variableSourceReads;
        }
        else
        {
            return new();
        }

    }

    /// <inheritdoc/>
    protected override async ValueTask<OperResult<byte[]>> ReadSourceAsync(VariableSourceRead deviceVariableSourceRead, CancellationToken cancellationToken)
    {
        DateTime time = DateTime.Now;
        var addresss = deviceVariableSourceRead.VariableRuntimes.Where(a => !a.RegisterAddress.IsNullOrEmpty()).Select(a => a.RegisterAddress!).ToArray();
        try
        {
            await ReadWriteLock.ReaderLockAsync(cancellationToken).ConfigureAwait(false);

            var result = await _plc.ReadJTokenValueAsync(addresss, cancellationToken).ConfigureAwait(false);
            foreach (var data in result)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    var data1 = deviceVariableSourceRead.VariableRuntimes.Where(a => a.RegisterAddress == data.Item1);

                    foreach (var item in data1)
                    {
                        object value;
                        if (data.Item3 is JValue jValue)
                        {
                            value = jValue.Value;
                        }
                        else
                        {
                            value = data.Item3;
                        }

                        var isGood = StatusCode.IsGood(data.Item2.StatusCode);
                        if (_driverProperties.SourceTimestampEnable)
                        {
                            time = data.Item2.SourceTimestamp.ToLocalTime();
                        }
                        if (isGood)
                        {
                            item.SetValue(value, time);
                        }
                        else
                        {
                            if (item is VariableRuntime variable && (variable.IsOnline || variable.CollectTime == DateTime.UnixEpoch.ToLocalTime()))
                            {
                                LogMessage.LogWarning($"OPC quality bad:{Environment.NewLine}{data.Item1}");
                            }
                            item.SetValue(null, time, false);
                            item.VariableSource.LastErrorMessage = data.Item2.StatusCode.ToString();
                        }
                    }
                    LogMessage.Trace($"Change:{Environment.NewLine}{data.Item1} : {data.Item3}");
                }
            }

            return OperResult.CreateSuccessResult<byte[]>(null);
        }
        catch (Exception ex)
        {
            return new OperResult<byte[]>($"ReadSourceAsync {addresss.ToJsonNetString()}：{Environment.NewLine}{ex}");
        }
        finally
        {
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask<Dictionary<string, OperResult>> WriteValuesAsync(Dictionary<VariableRuntime, JToken> writeInfoLists, CancellationToken cancellationToken)
    {
        using var writeLock = ReadWriteLock.WriterLock();
        try
        {
            var result = await _plc.WriteNodeAsync(writeInfoLists.ToDictionary(a => a.Key.RegisterAddress!, a => a.Value), cancellationToken).ConfigureAwait(false);
            return result.ToDictionary<KeyValuePair<string, Tuple<bool, string>>, string, OperResult>(a =>
            {
                return writeInfoLists.Keys.FirstOrDefault(b => b.RegisterAddress == a.Key)?.Name!;
            }
            , a =>
            {
                if (!a.Value.Item1)
                    return new OperResult(a.Value.Item2);
                else
                    return OperResult.Success;
            })!;
        }
        finally
        {
        }
    }

    private void _plc_LogEvent(byte level, object sender, string message, Exception ex)
    {
        LogMessage?.Log((LogLevel)level, sender, message, ex);
    }

    public override async Task AfterVariablesChangedAsync(CancellationToken cancellationToken)
    {
        try
        {
            _plc?.Disconnect();
            await base.AfterVariablesChangedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            VariableAddresDicts = IdVariableRuntimes.Select(a => a.Value).Where(it => !it.RegisterAddress.IsNullOrEmpty()).GroupBy(a => a.RegisterAddress).ToDictionary(a => a.Key!, b => b.ToList());
            try
            {
                await _plc.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogMessage.LogWarning(ex);
            }
        }

    }

    private Dictionary<string, List<VariableRuntime>> VariableAddresDicts { get; set; } = new();

    private void DataChangedHandler((VariableNode variableNode, MonitoredItem monitoredItem, DataValue dataValue, JToken jToken) data)
    {
        DateTime time = DateTime.Now;
        try
        {
            if (CurrentDevice.Pause)
                return;
            if (DisposedValue)
                return;


            if (CurrentDevice.Pause)
            {
                return;
            }

            LogMessage.Trace($"Change: {Environment.NewLine} {data.monitoredItem.StartNodeId} : {data.jToken?.ToString()}");

            //尝试固定点位的数据类型
            var type = TypeInfo.GetSystemType(TypeInfo.GetBuiltInType(data.variableNode.DataType, _plc.Session.SystemContext.TypeTable), data.variableNode.ValueRank);

            if (!VariableAddresDicts.TryGetValue(data.monitoredItem.StartNodeId.ToString(), out var itemReads)) return;

            object value;
            if (data.jToken is JValue jValue)
            {
                value = jValue.Value;
            }
            else
            {
                value = data.jToken;
            }
            var isGood = StatusCode.IsGood(data.dataValue.StatusCode);
            if (_driverProperties.SourceTimestampEnable)
            {
                time = data.dataValue.SourceTimestamp.ToLocalTime();
            }
            foreach (var item in itemReads)
            {
                if (CurrentDevice.Pause)
                    return;
                if (DisposedValue)
                    return;
                if (item.DataType == DataTypeEnum.Object)
                    if (type.Namespace.StartsWith("System"))
                    {
                        var enumValues = Enum.GetValues<DataTypeEnum>();
                        var stringList = enumValues.Select(e => e.ToString());
                        if (stringList.Contains(type.Name))
                            try { item.DataType = Enum.Parse<DataTypeEnum>(type.Name); } catch { }
                    }
                if (isGood)
                {
                    item.SetValue(value, time);
                }
                else
                {
                    if ((item.IsOnline || item.CollectTime == DateTime.UnixEpoch.ToLocalTime()))
                    {
                        LogMessage.LogWarning($"OPC quality bad:{Environment.NewLine}{item.Name}");
                    }
                    item.SetValue(null, time, false);
                    item.VariableSource.LastErrorMessage = data.dataValue.StatusCode.ToString();
                }
            }
            success = true;
        }
        catch (Exception ex)
        {
            if (success)
                LogMessage?.LogWarning(ex);
            success = false;
        }
    }
}

﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using MQTTnet;


#if NET6_0
using MQTTnet.Client;
#endif

using ThingsGateway.Foundation;
using ThingsGateway.NewLife.Threading;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.Mqtt;

/// <summary>
/// MqttCollect,RPC方法适配mqttNet
/// </summary>
public partial class MqttCollect : CollectBase
{
    private readonly MqttCollectProperty _driverPropertys = new();
    public override CollectPropertyBase CollectProperties => _driverPropertys;

    /// <inheritdoc/>
    public override bool IsConnected() => _mqttClient?.IsConnected == true;

    /// <inheritdoc/>
    public override string ToString()
    {
        return $" {nameof(MqttClient)} IP:{_driverPropertys.IP} Port:{_driverPropertys.Port}";
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _mqttClient?.SafeDispose();
        TopicItemDict?.Clear();
        base.Dispose(disposing);
    }

    public override string GetAddressDescription()
    {
        return """
                变量地址：${mqtt_topic};${payload_item};${Condition}
                主题：vendor/device
                负载示例：
                {
                     "ModuleUnoccupied": {
                          "EquipId":"E12",
                          "CarrierId": "C12",
                          "SubstrateLocId": "S12",
                          "LotId": 1,
                          "DesignId": "D12",
                          "EventTime": "12322131"
                     }
                }

               比如vendor/device;ModuleUnoccupied.EquipId，结果是"E12"
               比如vendor/device;ModuleUnoccupied.EquipId;((JToken)raw).SelectToken("ModuleUnoccupied.LotId").ToString().ToInt()==1，结果是"E12"
            
            """;
    }


    private Dictionary<string, List<Tuple<string, string, VariableRuntime>>> TopicItemDict = new();

    private sealed class TopicItem
    {
        public string Topic { get; set; }
        public string Item { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is TopicItem topicItem)
            {
                if (topicItem.Topic == Topic)
                {
                    return true;
                }
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + (Topic?.GetHashCode() ?? 0);
            return hash;
        }
    }

    protected override async Task<List<VariableSourceRead>> ProtectedLoadSourceReadAsync(List<VariableRuntime> deviceVariables)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        TopicItemDict.Clear();
        if (deviceVariables.Count > 0)
        {
            var dataResult = new List<VariableSourceRead>();

            var groups = deviceVariables.GroupBy(a =>
                 {
                     TopicItem topic = new();
                     try
                     {
                         var addressSplit = a.RegisterAddress.Split(';');
                         topic.Topic = addressSplit[0];
                     }
                     catch
                     {
                         LogMessage?.LogWarning($"Variable address format error：{a.RegisterAddress}");
                     }
                     return topic.Topic;
                 });

            //获取主题，负载路径list的字典
            foreach (var group in groups)
            {
                TopicItemDict.TryAdd(group.Key, new());
                TopicItemDict[group.Key] = new();
                var sourVars = new VariableSourceRead()
                {
                    TimeTick = new("1000"),
                    RegisterAddress = group.Key,
                };
                foreach (var item in group)
                {
                    try
                    {
                        var addressSplit = item.RegisterAddress.Split(';');
                        if (addressSplit.Length > 2)
                            TopicItemDict[group.Key].Add(new Tuple<string, string, VariableRuntime>(addressSplit[1], addressSplit[2], item));
                        else if (addressSplit.Length > 1)
                            TopicItemDict[group.Key].Add(new Tuple<string, string, VariableRuntime>(addressSplit[1], string.Empty, item));
                        else
                            TopicItemDict[group.Key].Add(new Tuple<string, string, VariableRuntime>(string.Empty, string.Empty, item));
                    }
                    catch
                    {
                        LogMessage?.LogWarning($"Variable address format error：{item.RegisterAddress}");
                    }
                    sourVars.AddVariable(item);
                }
                dataResult.Add(sourVars);
            }



            var mqttClientSubscribeOptionsBuilder = new MqttClientSubscribeOptionsBuilder();
            foreach (var item in TopicItemDict.Keys)
            {
                if (!item.IsNullOrWhiteSpace())
                {
                    mqttClientSubscribeOptionsBuilder = mqttClientSubscribeOptionsBuilder.WithTopicFilter(
                        f =>
                        {
                            f.WithTopic(item);
                        });
                }
                var mqttClientSubscribeOptions = mqttClientSubscribeOptionsBuilder.Build();
                if (mqttClientSubscribeOptions.TopicFilters.Count > 0)
                    _mqttSubscribeOptions = mqttClientSubscribeOptions;
            }

            return dataResult;
        }
        else
        {
            return new();
        }

    }

    protected override async Task InitChannelAsync(IChannel? channel, CancellationToken cancellationToken)
    {
        ETime = TimeSpan.FromSeconds(_driverPropertys.CheckClearTime);

        #region 初始化

#if NET8_0_OR_GREATER
        var mqttFactory = new MqttClientFactory();
        var mqttClientOptionsBuilder = mqttFactory.CreateClientOptionsBuilder()
           .WithClientId(_driverPropertys.ConnectId)
           .WithCredentials(_driverPropertys.UserName, _driverPropertys.Password)//账密
           .WithProtocolVersion(_driverPropertys.MqttProtocolVersion)
           .WithCleanSession(true)
           .WithKeepAlivePeriod(TimeSpan.FromSeconds(120.0));
#else

        var mqttFactory = new MqttFactory();
        var mqttClientOptionsBuilder = mqttFactory.CreateClientOptionsBuilder()
           .WithClientId(_driverPropertys.ConnectId)
           .WithCredentials(_driverPropertys.UserName, _driverPropertys.Password)//账密
           .WithCleanSession(true)
           .WithKeepAlivePeriod(TimeSpan.FromSeconds(120.0))
           .WithoutThrowOnNonSuccessfulConnectResponse();

#endif
        if (_driverPropertys.IsWebSocket)
            _mqttClientOptions = mqttClientOptionsBuilder.WithWebSocketServer(a => a.WithUri(_driverPropertys.WebSocketUrl))
           .Build();
        else
            _mqttClientOptions = mqttClientOptionsBuilder.WithTcpServer(_driverPropertys.IP, _driverPropertys.Port)//服务器
           .Build();


        _mqttClient = mqttFactory.CreateMqttClient();
        _mqttClient.ConnectedAsync += MqttClient_ConnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;

        #endregion 初始化

        await base.InitChannelAsync(channel, cancellationToken).ConfigureAwait(false);
    }

    private TimeSpan ETime = TimeSpan.FromSeconds(60000);
    protected override async Task ProtectedStartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested && !DisposedValue)
            {
                try
                {
                    foreach (var item in IdVariableRuntimes)
                    {
                        if (DateTime.Now - item.Value.CollectTime > ETime)
                        {
                            item.Value.SetValue(null, DateTime.Now, false);
                        }
                    }
                }
                finally
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }
        }, cancellationToken);

        await base.ProtectedStartAsync(cancellationToken).ConfigureAwait(false);
        if (_mqttClient != null)
        {
            var result = await TryMqttClientAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                return;
            if (!result.IsSuccess)
            {
                LogMessage?.LogWarning(result.Exception, $"{ToString()} Connect fail {result.ErrorMessage}");
            }
        }
    }


    private volatile bool success;
    protected override async ValueTask ProtectedExecuteAsync(CancellationToken cancellationToken)
    {
        var clientResult = await TryMqttClientAsync(cancellationToken).ConfigureAwait(false);
        if (!clientResult.IsSuccess)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            if (success != clientResult.IsSuccess)
            {
                if (!clientResult.IsSuccess)
                    LogMessage.LogWarning(clientResult.Exception, clientResult.ErrorMessage);
                success = clientResult.IsSuccess;
            }
            await Task.Delay(10000, cancellationToken).ConfigureAwait(false);
            //return;
        }
        //获取设备连接状态
        if (IsConnected())
        {
            //更新设备活动时间
            CurrentDevice.SetDeviceStatus(TimerX.Now, false);
        }
        else
        {
            CurrentDevice.SetDeviceStatus(TimerX.Now, true);
        }

        ScriptVariableRun(cancellationToken);

    }


}

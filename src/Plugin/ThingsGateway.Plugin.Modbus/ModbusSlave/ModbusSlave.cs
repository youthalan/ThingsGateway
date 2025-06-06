﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.Extensions.Localization;

using Newtonsoft.Json.Linq;

using SqlSugar;

using System.Collections.Concurrent;

using ThingsGateway.Extension;
using ThingsGateway.Foundation.Modbus;
using ThingsGateway.Gateway.Application;
using ThingsGateway.NewLife;
using ThingsGateway.NewLife.Extension;
using ThingsGateway.NewLife.Json.Extension;
using ThingsGateway.NewLife.Threading;

using TouchSocket.Core;
using TouchSocket.Sockets;

namespace ThingsGateway.Plugin.Modbus;

public class ModbusSlave : BusinessBase
{
    private readonly ModbusSlaveProperty _driverPropertys = new();

    private readonly ConcurrentQueue<(string, VariableRuntime)> _modbusVariableQueue = new();

    private readonly ModbusSlaveVariableProperty _variablePropertys = new();

    private Dictionary<ModbusAddress, VariableRuntime> ModbusVariables;

    private ThingsGateway.Foundation.Modbus.ModbusSlave _plc = new();

    private volatile bool success = true;

    /// <inheritdoc/>
    public override Type DriverDebugUIType => typeof(ThingsGateway.Debug.ModbusSlave);

    /// <inheritdoc/>
    public override Type DriverUIType
    {
        get
        {
            if (_plc.Channel?.ChannelType == ChannelTypeEnum.TcpService)
                return typeof(ThingsGateway.Gateway.Razor.TcpServiceComponent);
            else
                return null;
        }
    }

    /// <inheritdoc/>
    public override VariablePropertyBase VariablePropertys => _variablePropertys;

    /// <inheritdoc/>
    protected override BusinessPropertyBase _businessPropertyBase => _driverPropertys;

    protected IStringLocalizer Localizer { get; private set; }

    /// <summary>
    /// 是否连接成功
    /// </summary>
    public override bool IsConnected()
    {
        return _plc?.OnLine == true;
    }

    public override string ToString()
    {
        return _plc?.ToString() ?? base.ToString();
    }

    /// <summary>
    /// 开始通讯执行的方法
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override async Task ProtectedStartAsync(CancellationToken cancellationToken)
    {
        if (_plc?.Channel != null)
            await _plc.Channel.ConnectAsync(_plc.Channel.ChannelOptions.ConnectTimeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task InitChannelAsync(IChannel? channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        //载入配置
        _plc.DataFormat = _driverPropertys.DataFormat;
        _plc.IsStringReverseByteWord = _driverPropertys.IsStringReverseByteWord;
        _plc.Station = _driverPropertys.Station;
        _plc.IsWriteMemory = _driverPropertys.IsWriteMemory;
        _plc.MulStation = _driverPropertys.MulStation;
        _plc.ModbusType = _driverPropertys.ModbusType;
        _plc.InitChannel(channel, LogMessage);
        await base.InitChannelAsync(channel, cancellationToken).ConfigureAwait(false);

        _plc.WriteData -= OnWriteData;
        _plc.WriteData += OnWriteData;

        try
        {
            await _plc.Channel.ConnectAsync(_plc.Channel.ChannelOptions.ConnectTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }

        Localizer = App.CreateLocalizerByType(typeof(ModbusSlave))!;

        GlobalData.VariableValueChangeEvent -= VariableValueChange;
        GlobalData.VariableValueChangeEvent += VariableValueChange;

    }
    public override async Task AfterVariablesChangedAsync(CancellationToken cancellationToken)
    {
        await base.AfterVariablesChangedAsync(cancellationToken).ConfigureAwait(false);
        _modbusVariableQueue?.Clear();
        IdVariableRuntimes.ForEach(a =>
        {
            VariableValueChange(a.Value, default);
        });

        ModbusVariables = IdVariableRuntimes.ToDictionary(a =>
        {
            ModbusAddress address = _plc.GetModbusAddress(
                a.Value.GetPropertyValue(DeviceId,
                nameof(_variablePropertys.ServiceAddress)), _driverPropertys.Station, isCache: false);
            return address!;
        },
        a => a.Value
        );

    }
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        ModbusVariables?.Clear();
        _modbusVariableQueue?.Clear();
        GlobalData.VariableValueChangeEvent -= VariableValueChange;
        _plc?.SafeDispose();
        base.Dispose(disposing);
    }


    protected override async ValueTask ProtectedExecuteAsync(CancellationToken cancellationToken)
    {
        //获取设备连接状态
        if (IsConnected())
        {
            //更新设备活动时间
            CurrentDevice.SetDeviceStatus(TimerX.Now, false);
        }
        else
        {
            CurrentDevice.SetDeviceStatus(TimerX.Now, true);
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                await _plc.Channel.CloseAsync().ConfigureAwait(false);
                await _plc.Channel.ConnectAsync(3000, cancellationToken).ConfigureAwait(false);
                success = true;
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                if (success)
                    LogMessage.LogWarning(ex, Localizer["CanStartService"]);
                success = false;
                await Task.Delay(10000, cancellationToken).ConfigureAwait(false);
            }
        }
        var list = _modbusVariableQueue.ToListWithDequeue();
        foreach (var item in list)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            var type = item.Item2.GetPropertyValue(CurrentDevice.Id, nameof(ModbusSlaveVariableProperty.DataType));
            if (Enum.TryParse(type, out DataTypeEnum result))
            {
                await _plc.WriteAsync(item.Item1, JToken.FromObject(item.Item2.Value), result, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _plc.WriteAsync(item.Item1, JToken.FromObject(item.Item2.Value), item.Item2.DataType, cancellationToken).ConfigureAwait(false);
            }
        }

    }

    /// <summary>
    /// RPC写入
    /// </summary>
    private async ValueTask<OperResult> OnWriteData(ModbusRequest modbusRequest, IThingsGatewayBitConverter bitConverter, IChannel channel)
    {
        try
        {
            var tag = ModbusVariables.Where(a => a.Key?.StartAddress == modbusRequest.StartAddress && a.Key?.Station == modbusRequest.Station && a.Key?.FunctionCode == modbusRequest.FunctionCode);
            if (!tag.Any()) return OperResult.Success;
            if (!(tag.All(a => a.Value.GetPropertyValue(DeviceId, nameof(_variablePropertys.VariableRpcEnable)).ToBoolean(false) && _driverPropertys.DeviceRpcEnable)))
                return new OperResult(Localizer["NotWriteEnable"]);

            foreach (var item in tag)
            {

                var type = item.Value.GetPropertyValue(DeviceId, nameof(ModbusSlaveVariableProperty.DataType));
                var dType = Enum.TryParse(type, out DataTypeEnum dataType) ? dataType : item.Value.DataType;
                var addressStr = item.Value.GetPropertyValue(DeviceId, nameof(ModbusSlaveVariableProperty.ServiceAddress));

                var thingsGatewayBitConverter = bitConverter.GetTransByAddress(addressStr);

                var writeData = modbusRequest.Data.ToArray();

                var bitIndex = _plc.GetBitOffset(addressStr);
                if (modbusRequest.FunctionCode == 0x03 && dType == DataTypeEnum.Boolean && bitIndex != null)
                {
                    var int16Data = thingsGatewayBitConverter.ToUInt16(writeData, 0);
                    var wData = BitHelper.GetBit(int16Data, bitIndex.Value);

                    var result = await item.Value.RpcAsync(wData.ToJsonNetString(), $"{nameof(ModbusSlave)}-{CurrentDevice.Name}-{$"{channel}"}").ConfigureAwait(false);

                    if (!result.IsSuccess)
                        return result;

                }
                else
                {
                    var data = thingsGatewayBitConverter.GetDataFormBytes(_plc, addressStr, writeData, 0, dType, item.Value.ArrayLength ?? 1);

                    var result = await item.Value.RpcAsync(data.ToJsonNetString(), $"{nameof(ModbusSlave)}-{CurrentDevice.Name}-{$"{channel}"}").ConfigureAwait(false);

                    if (!result.IsSuccess)
                        return result;
                }
            }
            return OperResult.Success;
        }
        catch (Exception ex)
        {
            return new OperResult(ex);
        }
    }

    /// <inheritdoc/>
    private void VariableValueChange(VariableRuntime variableRuntime, VariableBasicData variableData)
    {
        if (CurrentDevice.Pause == true)
            return;
        var address = variableRuntime.GetPropertyValue(DeviceId, nameof(_variablePropertys.ServiceAddress));
        if (address != null && variableRuntime.Value != null)
        {
            _modbusVariableQueue?.Enqueue((address, variableRuntime));
        }
    }
}

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

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

using System.Security.Cryptography.X509Certificates;

using ThingsGateway.Extension;
using ThingsGateway.Foundation;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.Mqtt;

/// <summary>
/// MqttClient,RPC方法适配mqttNet
/// </summary>
public partial class MqttClient : BusinessBaseWithCacheIntervalScript<VariableBasicData, DeviceBasicData, AlarmVariable>
{
    private readonly MqttClientProperty _driverPropertys = new();
    private readonly MqttClientVariableProperty _variablePropertys = new();
    public override VariablePropertyBase VariablePropertys => _variablePropertys;
    protected override BusinessPropertyWithCacheIntervalScript _businessPropertyWithCacheIntervalScript => _driverPropertys;

    public override Type DriverPropertyUIType => typeof(MqttClientPropertyRazor);

    /// <summary>
    /// 加载 PEM 证书和私钥
    /// </summary>
    private static X509Certificate2 LoadCertificate(string certPath, string keyPath)
    {
        var cert = new X509Certificate2(certPath);

        using var reader = new StreamReader(keyPath);
        var pemReader = new PemReader(reader);
        var keyPair = pemReader.ReadObject() as AsymmetricCipherKeyPair;

        if (keyPair == null)
        {
            throw new Exception("Invalid private key.");
        }

        var rsaPrivateKey = DotNetUtilities.ToRSA(keyPair.Private as Org.BouncyCastle.Crypto.Parameters.RsaPrivateCrtKeyParameters);
        var certWithKey = cert.CopyWithPrivateKey(rsaPrivateKey);

        return certWithKey;
    }

    protected override async Task InitChannelAsync(IChannel? channel, CancellationToken cancellationToken)
    {
        #region 初始化


#if NET8_0_OR_GREATER
        var mqttFactory = new MqttClientFactory();
        var mqttClientOptionsBuilder = mqttFactory.CreateClientOptionsBuilder()
           .WithClientId(_driverPropertys.ConnectId)
           .WithCredentials(_driverPropertys.UserName, _driverPropertys.Password)//账密
           .WithCleanSession(true)

           .WithProtocolVersion(_driverPropertys.MqttProtocolVersion)

           .WithKeepAlivePeriod(TimeSpan.FromSeconds(120.0));

        if (_driverPropertys.TLS)
        {
            var caCert = new X509Certificate2(_driverPropertys.CAFile);
            var clientCert = LoadCertificate(_driverPropertys.ClientCertificateFile, _driverPropertys.ClientKeyFile);
            mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithTlsOptions(a => a
                    .WithTrustChain(new X509Certificate2Collection(caCert))
                    .WithClientCertificates(new X509Certificate2Collection(clientCert))
                    .WithCertificateValidationHandler((a) =>
                    {
                        return true;
                    })
                    );
        }

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

        var mqttClientSubscribeOptionsBuilder = mqttFactory.CreateSubscribeOptionsBuilder();
        if (!_driverPropertys.RpcWriteTopic.IsNullOrWhiteSpace())
        {
            if (_driverPropertys.RpcWriteTopic == ThingsBoardRpcTopic)
            {
                mqttClientSubscribeOptionsBuilder = mqttClientSubscribeOptionsBuilder.WithTopicFilter(
     f =>
     {
         f.WithTopic(_driverPropertys.RpcWriteTopic);
     });
            }
            else
            {
                mqttClientSubscribeOptionsBuilder = mqttClientSubscribeOptionsBuilder.WithTopicFilter(
                    f =>
                    {
                        f.WithTopic(string.Format(null, RpcTopic, _driverPropertys.RpcWriteTopic));
                    });
            }
        }
        if (!_driverPropertys.RpcQuestTopic.IsNullOrWhiteSpace())
        {
            mqttClientSubscribeOptionsBuilder = mqttClientSubscribeOptionsBuilder.WithTopicFilter(
                f =>
                {
                    f.WithTopic(_driverPropertys.RpcQuestTopic);
                });
        }
        var mqttClientSubscribeOptions = mqttClientSubscribeOptionsBuilder.Build();
        if (mqttClientSubscribeOptions.TopicFilters.Count > 0)
            _mqttSubscribeOptions = mqttClientSubscribeOptions;

        _mqttClient = mqttFactory.CreateMqttClient();
        _mqttClient.ConnectedAsync += MqttClient_ConnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;

        #endregion 初始化
        await base.InitChannelAsync(channel, cancellationToken).ConfigureAwait(false);
    }

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
        base.Dispose(disposing);
    }

    protected override async Task ProtectedStartAsync(CancellationToken cancellationToken)
    {
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
        //TD设备上线

        var data = ThingsBoardDeviceConnectQueue.ToListWithDequeue();
        if (data?.Count > 0)
        {
            foreach (var item in data)
            {
                await UpdateThingsBoardDeviceConnect(item).ConfigureAwait(false);
            }
        }


        await Update(cancellationToken).ConfigureAwait(false);

    }
}

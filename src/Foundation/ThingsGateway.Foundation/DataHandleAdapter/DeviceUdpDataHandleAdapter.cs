﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Net;
using System.Text;

namespace ThingsGateway.Foundation;

/// <summary>
/// UDP适配器基类
/// </summary>
public class DeviceUdpDataHandleAdapter<TRequest> : UdpDataHandlingAdapter where TRequest : MessageBase, new()
{
    /// <inheritdoc/>
    public override bool CanSendRequestInfo => true;

    /// <inheritdoc/>
    public override bool CanSplicingSend => false;

    /// <summary>
    /// 报文输出时采用字符串还是HexString
    /// </summary>
    public virtual bool IsHexLog { get; set; } = true;

    public virtual bool IsSingleThread { get; set; } = true;

    /// <summary>
    /// 非并发协议中，每次交互的对象，会在发送时重新获取
    /// </summary>
    public TRequest Request { get; set; }

    /// <inheritdoc />
    public void SetRequest(ISendMessage sendMessage, ref ValueByteBlock byteBlock)
    {
        var request = GetInstance();
        request.Sign = sendMessage.Sign;
        request.SendInfo(sendMessage, ref byteBlock);
        Request = request;
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        return Owner.ToString();
    }

    /// <summary>
    /// 获取泛型实例。
    /// </summary>
    /// <returns></returns>
    protected virtual TRequest GetInstance()
    {
        return new TRequest() { OperCode = -1, Sign = -1 };
    }

    /// <inheritdoc/>
    protected override async Task PreviewReceived(EndPoint remoteEndPoint, ByteBlock byteBlock)
    {
        try
        {
            byteBlock.Position = 0;

            if (Logger?.LogLevel <= LogLevel.Trace)
                Logger?.Trace($"{remoteEndPoint}- Receive:{(IsHexLog ? byteBlock.AsSegmentTake().ToHexString() : byteBlock.ToString(byteBlock.Position))}");

            TRequest request = null;
            if (IsSingleThread)
                request = Request == null ? GetInstance() : Request;
            else
            {
                request = GetInstance();
            }

            var pos = byteBlock.Position;

            if (request.HeaderLength > byteBlock.CanReadLength)
            {
                return;//当头部都无法解析时，直接缓存
            }

            //检查头部合法性
            if (request.CheckHead(ref byteBlock))
            {
                byteBlock.Position = pos;

                if (request.BodyLength > MaxPackageSize)
                {
                    OnError(default, $"Received BodyLength={request.BodyLength}, greater than the set MaxPackageSize={MaxPackageSize}", true, true);
                    return;
                }
                if (request.BodyLength + request.HeaderLength > byteBlock.CanReadLength)
                {
                    //body不满足解析，开始缓存，然后保存对象
                    return;
                }
                //if (request.BodyLength <= 0)
                //{
                //    //如果body长度无法确定，直接读取全部
                //    request.BodyLength = byteBlock.Length;
                //}
                var headPos = pos + request.HeaderLength;
                byteBlock.Position = headPos;
                var result = request.CheckBody(ref byteBlock);
                if (result == FilterResult.Cache)
                {
                    if (Logger?.LogLevel <= LogLevel.Trace)
                        Logger?.Trace($"{ToString()}-Received incomplete, cached message, current length:{byteBlock.Length}  {request?.ErrorMessage}");
                    request.OperCode = -1;
                }
                else if (result == FilterResult.GoOn)
                {
                    var addLen = request.HeaderLength + request.BodyLength;
                    byteBlock.Position = pos + (addLen > 0 ? addLen : 1);
                    Logger?.Trace($"{ToString()}-{request?.ToString()}");
                    request.OperCode = -1;
                    if ((Owner as IClientChannel)?.WaitHandlePool?.TryGetDataAsync(request.Sign, out var waitDataAsync) == true)
                    {
                        waitDataAsync.SetResult(request);
                    }
                }
                else if (result == FilterResult.Success)
                {
                    var addLen = request.HeaderLength + request.BodyLength;
                    byteBlock.Position = pos + (addLen > 0 ? addLen : 1);
                    await GoReceived(remoteEndPoint, null, request).ConfigureAwait(false);
                }
                return;
            }
            else
            {
                byteBlock.Position = pos + 1;
                request.OperCode = -1;
                return;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, $"{ToString()} Received parsing error");
            byteBlock.Position = byteBlock.Length;//移动游标
            return;
        }
    }

    /// <inheritdoc/>
    protected override async Task PreviewSendAsync(EndPoint endPoint, ReadOnlyMemory<byte> memory)
    {
        if (Logger?.LogLevel <= LogLevel.Trace)
            Logger?.Trace($"{ToString()}- Send:{(IsHexLog ? memory.Span.ToHexString() : (memory.Span.ToString(Encoding.UTF8)))}");

        //发送
        await GoSendAsync(endPoint, memory).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task PreviewSendAsync(EndPoint endPoint, IRequestInfo requestInfo)
    {
        if (!(requestInfo is ISendMessage sendMessage))
        {
            throw new Exception($"Unable to convert {nameof(requestInfo)} to {nameof(ISendMessage)}");
        }


        var byteBlock = new ValueByteBlock(sendMessage.MaxLength);
        try
        {
            sendMessage.Build(ref byteBlock);
            if (Logger?.LogLevel <= LogLevel.Trace)
                Logger?.Trace($"{endPoint}- Send:{(IsHexLog ? byteBlock.Span.ToHexString() : (byteBlock.Span.ToString(Encoding.UTF8)))}");

            if (IsSingleThread)
            {
                SetRequest(sendMessage, ref byteBlock);
            }
            await GoSendAsync(endPoint, byteBlock.Memory).ConfigureAwait(false);
        }
        finally
        {
            byteBlock.SafeDispose();
        }
    }
}

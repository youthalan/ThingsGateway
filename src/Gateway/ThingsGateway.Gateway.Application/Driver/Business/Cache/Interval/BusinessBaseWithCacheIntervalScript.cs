﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

using ThingsGateway.NewLife.Json.Extension;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

/// <summary>
/// 业务插件，额外实现脚本切换实体
/// </summary>
public abstract partial class BusinessBaseWithCacheIntervalScript<VarModel, DevModel, AlarmModel> : BusinessBaseWithCacheIntervalAlarmModel<VarModel, DevModel, AlarmModel>
{
    protected sealed override BusinessPropertyWithCacheInterval _businessPropertyWithCacheInterval => _businessPropertyWithCacheIntervalScript;

    protected abstract BusinessPropertyWithCacheIntervalScript _businessPropertyWithCacheIntervalScript { get; }

    public virtual List<string> Match(string input)
    {
        // 生成缓存键，以确保缓存的唯一性
        var cacheKey = $"{nameof(BusinessBaseWithCacheIntervalScript<VarModel, DevModel, AlarmModel>)}-{CultureInfo.CurrentUICulture.Name}-Match-{input}";

        // 尝试从缓存中获取匹配结果，如果缓存中不存在则创建新的匹配结果
        var strings = App.CacheService.GetOrAdd(cacheKey, entry =>
        {
            List<string> strings = new List<string>();

            // 使用正则表达式查找输入字符串中的所有匹配项
            Regex regex = TopicRegex();
            MatchCollection matches = regex.Matches(input);

            // 遍历匹配结果，将匹配到的字符串添加到列表中
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    string productKey = match.Groups[1].Value;
                    strings.Add(productKey);
                }
            }

            // 返回匹配结果列表
            return strings;
        });

        // 返回匹配结果列表
        return strings;
    }

    #region 封装方法

    protected List<TopicJson> GetAlarms(IEnumerable<AlarmModel> item)
    {
        var data = Application.DynamicModelExtension.GetDynamicModel<AlarmModel>(item, _businessPropertyWithCacheIntervalScript.BigTextScriptAlarmModel);
        var topicJsonList = new List<TopicJson>();
        var topics = Match(_businessPropertyWithCacheIntervalScript.AlarmTopic);
        if (topics.Count > 0)
        {
            {
                //获取分组最终结果
                var groups = data.GroupByKeys(topics);

                foreach (var group in groups)
                {
                    // 上传主题
                    // 获取预定义的报警主题
                    string topic = _businessPropertyWithCacheIntervalScript.AlarmTopic;

                    // 将主题中的占位符替换为分组键对应的值
                    for (int i = 0; i < topics.Count; i++)
                    {
                        topic = topic.Replace(@"${" + topics[i] + @"}", group.Key[i]?.ToString());
                    }

                    // 上传内容
                    if (_businessPropertyWithCacheIntervalScript.IsAlarmList)
                    {

                        // 如果是报警列表，则将整个分组转换为 JSON 字符串
                        var gList = group.Select(a => a).ToList();
                        string json = gList.ToJsonNetString(_businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                        // 将主题和 JSON 内容添加到列表中
                        topicJsonList.Add(new(topic, json, gList.Count));
                    }
                    else
                    {
                        // 如果不是报警列表，则将每个分组元素分别转换为 JSON 字符串
                        foreach (var gro in group)
                        {
                            string json = JsonExtensions.ToJsonNetString(gro, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                            // 将主题和 JSON 内容添加到列表中
                            topicJsonList.Add(new(topic, json, 1));
                        }
                    }
                }
            }
        }
        else
        {
            if (_businessPropertyWithCacheIntervalScript.IsAlarmList)
            {
                var gList = data.Select(a => a).ToList();
                string json = gList.ToJsonNetString(_businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                topicJsonList.Add(new(_businessPropertyWithCacheIntervalScript.AlarmTopic, json, gList.Count));
            }
            else
            {
                foreach (var group in data)
                {
                    string json = JsonExtensions.ToJsonNetString(group, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                    topicJsonList.Add(new(_businessPropertyWithCacheIntervalScript.AlarmTopic, json, 1));
                }
            }
        }

        return topicJsonList;
    }


    protected List<TopicJson> GetDeviceData(IEnumerable<DevModel> item)
    {
        var data = Application.DynamicModelExtension.GetDynamicModel<DevModel>(item, _businessPropertyWithCacheIntervalScript.BigTextScriptDeviceModel);
        var topicJsonList = new List<TopicJson>();
        var topics = Match(_businessPropertyWithCacheIntervalScript.DeviceTopic);
        if (topics.Count > 0)
        {
            {
                //获取分组最终结果
                var groups = data.GroupByKeys(topics).ToList();
                if (groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        // 上传主题
                        // 获取预定义的设备主题
                        string topic = _businessPropertyWithCacheIntervalScript.DeviceTopic;

                        // 将主题中的占位符替换为分组键对应的值
                        for (int i = 0; i < topics.Count; i++)
                        {
                            topic = topic.Replace(@"${" + topics[i] + @"}", group.Key[i]?.ToString());
                        }

                        // 上传内容
                        if (_businessPropertyWithCacheIntervalScript.IsDeviceList)
                        {
                            // 如果是设备列表，则将整个分组转换为 JSON 字符串
                            var gList = group.Select(a => a).ToList();
                            string json = gList.ToJsonNetString(_businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                            // 将主题和 JSON 内容添加到列表中
                            topicJsonList.Add(new(topic, json, gList.Count));
                        }
                        else
                        {
                            // 如果不是设备列表，则将每个分组元素分别转换为 JSON 字符串
                            foreach (var gro in group)
                            {
                                string json = JsonExtensions.ToJsonNetString(gro, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                                // 将主题和 JSON 内容添加到列表中
                                topicJsonList.Add(new(topic, json, 1));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (_businessPropertyWithCacheIntervalScript.IsDeviceList)
            {
                var gList = data.Select(a => a).ToList();
                string json = gList.ToJsonNetString(_businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                topicJsonList.Add(new(_businessPropertyWithCacheIntervalScript.DeviceTopic, json, gList.Count));
            }
            else
            {
                foreach (var group in data)
                {
                    string json = JsonExtensions.ToJsonNetString(group, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                    topicJsonList.Add(new(_businessPropertyWithCacheIntervalScript.DeviceTopic, json, 1));
                }
            }
        }
        return topicJsonList;
    }

    protected List<TopicJson> GetVariable(IEnumerable<VarModel> item)
    {
        var data = Application.DynamicModelExtension.GetDynamicModel<VarModel>(item, _businessPropertyWithCacheIntervalScript.BigTextScriptVariableModel);
        var topicJsonList = new List<TopicJson>();
        var topics = Match(_businessPropertyWithCacheIntervalScript.VariableTopic);
        if (topics.Count > 0)
        {
            {
                //获取分组最终结果
                var groups = data.GroupByKeys(topics).ToList();
                if (groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        // 上传主题
                        // 获取预定义的变量主题
                        string topic = _businessPropertyWithCacheIntervalScript.VariableTopic;

                        // 将主题中的占位符替换为分组键对应的值
                        for (int i = 0; i < topics.Count; i++)
                        {
                            topic = topic.Replace(@"${" + topics[i] + @"}", group.Key[i]?.ToString());
                        }

                        // 上传内容
                        if (_businessPropertyWithCacheIntervalScript.IsVariableList)
                        {
                            // 如果是变量列表，则将整个分组转换为 JSON 字符串
                            var gList = group.Select(a => a).ToList();
                            string json = gList.ToJsonNetString(_businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                            // 将主题和 JSON 内容添加到列表中
                            topicJsonList.Add(new(topic, json, gList.Count));
                        }
                        else
                        {
                            // 如果不是变量列表，则将每个分组元素分别转换为 JSON 字符串
                            foreach (var gro in group)
                            {
                                string json = JsonExtensions.ToJsonNetString(gro, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                                // 将主题和 JSON 内容添加到列表中
                                topicJsonList.Add(new(topic, json, 1));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (_businessPropertyWithCacheIntervalScript.IsVariableList)
            {
                var gList = data.Select(a => a).ToList();
                string json = gList.ToJsonNetString(_businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                topicJsonList.Add(new(_businessPropertyWithCacheIntervalScript.VariableTopic, json, gList.Count));
            }
            else
            {
                foreach (var group in data)
                {
                    string json = JsonExtensions.ToJsonNetString(group, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                    topicJsonList.Add(new(_businessPropertyWithCacheIntervalScript.VariableTopic, json, 1));
                }
            }
        }
        return topicJsonList;
    }


    protected List<TopicJson> GetVariableBasicData(IEnumerable<VariableBasicData> item)
    {
        IEnumerable<VariableBasicData>? data = null;
        if (!_businessPropertyWithCacheIntervalScript.BigTextScriptVariableModel.IsNullOrWhiteSpace())
        {
            return GetVariable(item.Cast<VarModel>());
        }
        else
        {
            data = item;
        }
        var topicJsonList = new List<TopicJson>();
        var topics = Match(_businessPropertyWithCacheIntervalScript.VariableTopic);
        if (topics.Count > 0)
        {
            {
                //获取分组最终结果
                var groups = data.GroupByKeys(topics).ToList();
                if (groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        // 上传主题
                        // 获取预定义的变量主题
                        string topic = _businessPropertyWithCacheIntervalScript.VariableTopic;

                        // 将主题中的占位符替换为分组键对应的值
                        for (int i = 0; i < topics.Count; i++)
                        {
                            topic = topic.Replace(@"${" + topics[i] + @"}", group.Key[i]?.ToString());
                        }

                        // 上传内容
                        if (_businessPropertyWithCacheIntervalScript.IsVariableList)
                        {

                            // 如果是变量列表，则将整个分组转换为 JSON 字符串
                            string json = group.Select(a => a).GroupBy(a => a.DeviceName, b => b).ToDictionary(a => a.Key, b => b.ToList()).ToJsonNetString(_businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                            // 将主题和 JSON 内容添加到列表中
                            topicJsonList.Add(new(topic, json, group.Count()));
                        }
                        else
                        {
                            // 如果不是变量列表，则将每个分组元素分别转换为 JSON 字符串
                            foreach (var gro in group)
                            {
                                string json = JsonExtensions.ToJsonNetString(gro, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                                // 将主题和 JSON 内容添加到列表中
                                topicJsonList.Add(new(topic, json, 1));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (_businessPropertyWithCacheIntervalScript.IsVariableList)
            {
                string json = data.Select(a => a).GroupBy(a => a.DeviceName, b => b).ToDictionary(a => a.Key, b => b.ToList()).ToJsonNetString(_businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                topicJsonList.Add(new(_businessPropertyWithCacheIntervalScript.VariableTopic, json, data.Count()));
            }
            else
            {
                foreach (var group in data)
                {
                    string json = JsonExtensions.ToJsonNetString(group, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                    topicJsonList.Add(new(_businessPropertyWithCacheIntervalScript.VariableTopic, json, 1));
                }
            }
        }
        return topicJsonList;
    }

    //protected static byte[] Serialize(object value)
    //{
    //    var block = new ValueByteBlock(1024 * 64);
    //    try
    //    {
    //        //将数据序列化到内存块
    //        FastBinaryFormatter.Serialize(ref block, value);
    //        block.SeekToStart();
    //        return block.Memory.GetArray().Array;
    //    }
    //    finally
    //    {
    //        block.Dispose();
    //    }
    //}
    protected static JsonSerializerOptions NoWriteIndentedJsonSerializerOptions = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };
    protected static JsonSerializerOptions WriteIndentedJsonSerializerOptions = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    protected static byte[] Serialize(object data, bool writeIndented)
    {
        if (data == null) return Array.Empty<byte>();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(data, data.GetType(), writeIndented ? WriteIndentedJsonSerializerOptions : NoWriteIndentedJsonSerializerOptions);
        return payload;
    }


    protected List<TopicArray> GetAlarmTopicArrays(IEnumerable<AlarmModel> item)
    {
        var data = Application.DynamicModelExtension.GetDynamicModel<AlarmModel>(item, _businessPropertyWithCacheIntervalScript.BigTextScriptAlarmModel);
        List<TopicArray> topicArrayList = new List<TopicArray>();
        var topics = Match(_businessPropertyWithCacheIntervalScript.AlarmTopic);
        if (topics.Count > 0)
        {
            {
                //获取分组最终结果
                var groups = data.GroupByKeys(topics);

                foreach (var group in groups)
                {
                    // 上传主题
                    // 获取预定义的报警主题
                    string topic = _businessPropertyWithCacheIntervalScript.AlarmTopic;

                    // 将主题中的占位符替换为分组键对应的值
                    for (int i = 0; i < topics.Count; i++)
                    {
                        topic = topic.Replace(@"${" + topics[i] + @"}", group.Key[i]?.ToString());
                    }

                    // 上传内容
                    if (_businessPropertyWithCacheIntervalScript.IsAlarmList)
                    {
                        var gList = group.Select(a => a).ToList();
                        var json = Serialize(gList, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                        // 将主题和 JSON 内容添加到列表中
                        topicArrayList.Add(new(topic, json, gList.Count));
                    }
                    else
                    {
                        // 如果不是报警列表，则将每个分组元素分别转换为 JSON 字符串
                        foreach (var gro in group)
                        {
                            var json = Serialize(gro, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                            // 将主题和 JSON 内容添加到列表中
                            topicArrayList.Add(new(topic, json, 1));
                        }
                    }
                }
            }
        }
        else
        {
            if (_businessPropertyWithCacheIntervalScript.IsAlarmList)
            {
                var gList = data.Select(a => a).ToList();
                var json = Serialize(gList, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                topicArrayList.Add(new(_businessPropertyWithCacheIntervalScript.AlarmTopic, json, gList.Count));
            }
            else
            {
                foreach (var group in data)
                {
                    var json = Serialize(group, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                    topicArrayList.Add(new(_businessPropertyWithCacheIntervalScript.AlarmTopic, json, 1));
                }
            }
        }

        return topicArrayList;
    }

    protected List<TopicArray> GetDeviceTopicArray(IEnumerable<DevModel> item)
    {
        var data = Application.DynamicModelExtension.GetDynamicModel<DevModel>(item, _businessPropertyWithCacheIntervalScript.BigTextScriptDeviceModel);
        List<TopicArray> topicArrayList = new List<TopicArray>();
        var topics = Match(_businessPropertyWithCacheIntervalScript.DeviceTopic);
        if (topics.Count > 0)
        {
            {
                //获取分组最终结果
                var groups = data.GroupByKeys(topics).ToList();
                if (groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        // 上传主题
                        // 获取预定义的设备主题
                        string topic = _businessPropertyWithCacheIntervalScript.DeviceTopic;

                        // 将主题中的占位符替换为分组键对应的值
                        for (int i = 0; i < topics.Count; i++)
                        {
                            topic = topic.Replace(@"${" + topics[i] + @"}", group.Key[i]?.ToString());
                        }

                        // 上传内容
                        if (_businessPropertyWithCacheIntervalScript.IsDeviceList)
                        {
                            // 如果是设备列表，则将整个分组转换为 JSON 字符串
                            var gList = group.Select(a => a).ToList();
                            var json = Serialize(gList, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                            // 将主题和 JSON 内容添加到列表中
                            topicArrayList.Add(new(topic, json, gList.Count));
                        }
                        else
                        {
                            // 如果不是设备列表，则将每个分组元素分别转换为 JSON 字符串
                            foreach (var gro in group)
                            {
                                var json = Serialize(gro, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                                // 将主题和 JSON 内容添加到列表中
                                topicArrayList.Add(new(topic, json, 1));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (_businessPropertyWithCacheIntervalScript.IsDeviceList)
            {
                var gList = data.Select(a => a).ToList();
                var json = Serialize(gList, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                topicArrayList.Add(new(_businessPropertyWithCacheIntervalScript.DeviceTopic, json, gList.Count));
            }
            else
            {
                foreach (var group in data)
                {
                    var json = Serialize(group, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                    topicArrayList.Add(new(_businessPropertyWithCacheIntervalScript.DeviceTopic, json, 1));
                }
            }
        }
        return topicArrayList;
    }

    protected List<TopicArray> GetVariableTopicArray(IEnumerable<VarModel> item)
    {
        var data = Application.DynamicModelExtension.GetDynamicModel<VarModel>(item, _businessPropertyWithCacheIntervalScript.BigTextScriptVariableModel);
        List<TopicArray> topicArrayList = new List<TopicArray>();
        var topics = Match(_businessPropertyWithCacheIntervalScript.VariableTopic);
        if (topics.Count > 0)
        {
            {
                //获取分组最终结果
                var groups = data.GroupByKeys(topics).ToList();
                if (groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        // 上传主题
                        // 获取预定义的变量主题
                        string topic = _businessPropertyWithCacheIntervalScript.VariableTopic;

                        // 将主题中的占位符替换为分组键对应的值
                        for (int i = 0; i < topics.Count; i++)
                        {
                            topic = topic.Replace(@"${" + topics[i] + @"}", group.Key[i]?.ToString());
                        }

                        // 上传内容
                        if (_businessPropertyWithCacheIntervalScript.IsVariableList)
                        {
                            // 如果是变量列表，则将整个分组转换为 JSON 字符串
                            var gList = group.Select(a => a).ToList();
                            var json = Serialize(gList, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                            // 将主题和 JSON 内容添加到列表中
                            topicArrayList.Add(new(topic, json, gList.Count));
                        }
                        else
                        {
                            // 如果不是变量列表，则将每个分组元素分别转换为 JSON 字符串
                            foreach (var gro in group)
                            {
                                var json = Serialize(gro, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                                // 将主题和 JSON 内容添加到列表中
                                topicArrayList.Add(new(topic, json, 1));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (_businessPropertyWithCacheIntervalScript.IsVariableList)
            {
                var gList = data.Select(a => a).ToList();
                var json = Serialize(gList, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                topicArrayList.Add(new(_businessPropertyWithCacheIntervalScript.VariableTopic, json, gList.Count));
            }
            else
            {
                foreach (var group in data)
                {
                    var json = Serialize(group, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                    topicArrayList.Add(new(_businessPropertyWithCacheIntervalScript.VariableTopic, json, 1));
                }
            }
        }
        return topicArrayList;
    }

    protected List<TopicArray> GetVariableBasicDataTopicArray(IEnumerable<VariableBasicData> item)
    {
        IEnumerable<VariableBasicData>? data = null;
        if (!_businessPropertyWithCacheIntervalScript.BigTextScriptVariableModel.IsNullOrWhiteSpace())
        {
            return GetVariableTopicArray(item.Cast<VarModel>());
        }
        else
        {
            data = item;
        }

        List<TopicArray> topicArrayList = new List<TopicArray>();
        var topics = Match(_businessPropertyWithCacheIntervalScript.VariableTopic);
        if (topics.Count > 0)
        {
            {
                //获取分组最终结果
                var groups = data.GroupByKeys(topics).ToList();
                if (groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        // 上传主题
                        // 获取预定义的变量主题
                        string topic = _businessPropertyWithCacheIntervalScript.VariableTopic;

                        // 将主题中的占位符替换为分组键对应的值
                        for (int i = 0; i < topics.Count; i++)
                        {
                            topic = topic.Replace(@"${" + topics[i] + @"}", group.Key[i]?.ToString());
                        }

                        // 上传内容
                        if (_businessPropertyWithCacheIntervalScript.IsVariableList)
                        {
                            // 如果是变量列表，则将整个分组转换为 JSON 字符串
                            var gList = group.Select(a => a).ToList();
                            var json = Serialize(gList, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                            // 将主题和 JSON 内容添加到列表中
                            topicArrayList.Add(new(topic, json, gList.Count));
                        }
                        else
                        {
                            // 如果不是变量列表，则将每个分组元素分别转换为 JSON 字符串
                            foreach (var gro in group)
                            {
                                var json = Serialize(gro, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                                // 将主题和 JSON 内容添加到列表中
                                topicArrayList.Add(new(topic, json, 1));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (_businessPropertyWithCacheIntervalScript.IsVariableList)
            {
                var gList = data.Select(a => a).ToList();
                var json = Serialize(gList, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                topicArrayList.Add(new(_businessPropertyWithCacheIntervalScript.VariableTopic, json, gList.Count));
            }
            else
            {
                foreach (var group in data)
                {
                    var json = Serialize(group, _businessPropertyWithCacheIntervalScript.JsonFormattingIndented);
                    topicArrayList.Add(new(_businessPropertyWithCacheIntervalScript.VariableTopic, json, 1));
                }
            }
        }
        return topicArrayList;
    }


    protected string GetDetailLogString(TopicArray topicArray, int queueCount)
    {
        if (queueCount > 0)
            return $"Up Topic：{topicArray.Topic}{Environment.NewLine}PayLoad：{Encoding.UTF8.GetString(topicArray.Json)} {Environment.NewLine} VarModelQueue:{queueCount}";
        else
            return $"Up Topic：{topicArray.Topic}{Environment.NewLine}PayLoad：{Encoding.UTF8.GetString(topicArray.Json)}";
    }

    protected string GetCountLogString(TopicArray topicArray, int queueCount)
    {
        if (queueCount > 0)
            return $"Up Topic：{topicArray.Topic}{Environment.NewLine}Count：{topicArray.Count} {Environment.NewLine} VarModelQueue:{queueCount}";
        else
            return $"Up Topic：{topicArray.Topic}{Environment.NewLine}Count：{topicArray.Count}";

    }
    [GeneratedRegex(@"\$\{(.+?)\}")]
    private static partial Regex TopicRegex();
    #endregion 封装方法
}

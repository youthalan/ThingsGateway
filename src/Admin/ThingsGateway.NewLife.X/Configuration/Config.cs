﻿using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;

using ThingsGateway.NewLife.Log;

namespace ThingsGateway.NewLife.Configuration;

/// <summary>配置文件基类</summary>
/// <remarks>
/// 标准用法：TConfig.Current
/// 
/// 配置实体类通过<see cref="ConfigAttribute"/>特性指定配置文件路径。
/// Current将加载配置文件，如果文件不存在或者加载失败，将实例化一个对象返回。
/// </remarks>
/// <typeparam name="TConfig"></typeparam>
public class Config<TConfig> where TConfig : Config<TConfig>, new()
{
    #region 静态
    /// <summary>当前使用的提供者</summary>
    public static IConfigProvider? Provider { get; set; }

    static Config()
    {
        // 创建提供者
        var att = typeof(TConfig).GetCustomAttribute<ConfigAttribute>(true);
        var value = att?.Name;
        if (value.IsNullOrEmpty())
        {
            value = typeof(TConfig).Name;
            if (value.EndsWith("Config") && value != "Config") value = value.TrimEnd("Config");
            if (value.EndsWith("Setting") && value != "Setting") value = value.TrimEnd("Setting");
        }
        var prv = ConfigProvider.Create(att?.Provider);
        if (prv is ConfigProvider prv2)
        {
            prv2.Init(value);
        }

        Provider = prv;
    }

    private static TConfig? _Current;
    /// <summary>当前实例。通过置空可以使其重新加载。</summary>
    public static TConfig Current
    {
        get
        {
            if (_Current != null) return _Current;
            lock (typeof(TConfig))
            {
                if (_Current != null) return _Current;

                var config = new TConfig();
                var prv = Provider ?? throw new ArgumentNullException(nameof(Provider));

                // 配置文件损坏时，要能够忽略错误，强行加载，避免影响系统正常运行
                try
                {
                    // 绑定提供者数据到配置对象
                    prv.Bind(config, true);
                }
                catch (Exception ex)
                {
                    XTrace.Log?.Error(ex.Message);
                }

                try
                {
                    config.OnLoaded();
                }
                catch (Exception ex)
                {
                    XTrace.Log?.Error(ex.Message);
                }

                try
                {
                    // OnLoad 中可能有变化，存回去
                    //prv.Save(config);
                    if (!prv.IsNew || Runtime.CreateConfigOnMissing) config.Save();
                }
                catch (Exception ex)
                {
                    XTrace.Log?.Error(ex.Message);
                }

                return _Current = config;
            }
        }
        set { _Current = value; }
    }
    #endregion

    #region 属性
    /// <summary>是否新的配置文件</summary>
    [XmlIgnore, IgnoreDataMember]
    //[Obsolete("=>_Provider.IsNew")]
    public Boolean IsNew => Provider?.IsNew ?? false;
    #endregion

    #region 成员方法
    /// <summary>从配置文件中读取完成后触发</summary>
    protected virtual void OnLoaded() { }

    /// <summary>保存到配置文件中去</summary>
    //[Obsolete("=>Provider.Save")]
    public virtual void Save() => Provider?.Save(this);
    #endregion
}
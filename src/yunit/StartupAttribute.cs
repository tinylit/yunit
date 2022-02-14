using System;
using Yunit.Sdk;

namespace Yunit
{
    /// <summary>
    /// 普通启动类。
    /// </summary>
    [StartupDriver(typeof(YStartupDriver))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StartupAttribute : Attribute
    {
        /// <summary>
        /// 启动类型，自动注入构造函数参数。
        /// </summary>
        public StartupAttribute() : this(typeof(YStartup))
        {
        }

        /// <summary>
        /// 启动类型。
        /// </summary>
        /// <param name="startupType">服务配置类.</param>
        /// <exception cref="ArgumentNullException"><paramref name="startupType"/> is null.</exception>
        public StartupAttribute(Type startupType)
        {
            if (startupType is null)
            {
                throw new ArgumentNullException(nameof(startupType));
            }

            if (startupType.IsInterface || startupType.IsAbstract)
            {
                throw new ArgumentException(nameof(startupType), $"{startupType}不是实现类!");
            }

            StartupType = startupType;
        }

        /// <summary>
        /// 启动类。
        /// </summary>
        public Type StartupType { get; }
    }
}

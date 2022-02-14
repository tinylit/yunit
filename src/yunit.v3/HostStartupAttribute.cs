using System;
using Yunit.Sdk;

namespace Yunit
{
    /// <summary>
    /// 调试站点。
    /// </summary>
    [StartupDriver(typeof(HostStartupDriver))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HostStartupAttribute : StartupAttribute
    {
        /// <summary>
        /// 启动类型。
        /// </summary>
        /// <param name="startupType">服务配置类.</param>
        /// <exception cref="ArgumentNullException"><paramref name="startupType"/> is null.</exception>
        public HostStartupAttribute(Type startupType) : base(startupType) { }
    }
}

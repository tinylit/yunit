using System;
using System.Collections.Generic;
using System.Text;

namespace Yunit
{
    /// <summary>
    /// 启动驱动类。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class StartupDriverAttribute : Attribute
    {
        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="startupDriverType"></param>
        public StartupDriverAttribute(Type startupDriverType)
        {
            if (startupDriverType is null)
            {
                throw new ArgumentNullException(nameof(startupDriverType));
            }

            Type iStartupDriverType = typeof(IStartupDriver);

            if (iStartupDriverType.IsAssignableFrom(startupDriverType))
            {
                StartupDriverType = startupDriverType;
            }
            else
            {
                throw new ArgumentException($"参数{startupDriverType.FullName}未实现{iStartupDriverType.FullName}接口！");
            }
        }

        public Type StartupDriverType { get; }
    }
}

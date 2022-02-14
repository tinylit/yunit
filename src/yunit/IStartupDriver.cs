using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace Yunit
{
    /// <summary>
    /// 启动驱动器。
    /// </summary>
    public interface IStartupDriver
    {
        /// <summary>
        /// 启动。
        /// </summary>
        /// <param name="attribute">启动类属性。</param>
        /// <returns><see cref="IHost"/>/<seealso cref="IWebHost"/></returns>
        IDisposable StartHost(StartupAttribute attribute);
    }
}

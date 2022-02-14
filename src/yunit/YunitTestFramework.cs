using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Yunit.Sdk
{
    //
    // 摘要:
    //     The implementation of Xunit.Abstractions.ITestFramework that supports discovery
    //     and execution of unit tests linked against xunit.core.dll, using xunit.execution.dll.
    public class YunitTestFramework : TestFramework
    {
        //
        // 摘要:
        //     Initializes a new instance of the Xunit.Sdk.XunitTestFramework class.
        //
        // 参数:
        //   messageSink:
        //     The message sink used to send diagnostic messages
        public YunitTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
        }

        protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo)
        {
            return new XunitTestFrameworkDiscoverer(assemblyInfo, base.SourceInformationProvider, base.DiagnosticMessageSink);
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new YunitTestFrameworkExecutor(assemblyName, base.SourceInformationProvider, base.DiagnosticMessageSink);
        }
    }
}

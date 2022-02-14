using System.Collections.Generic;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Yunit.Sdk
{
    //
    // 摘要:
    //     The implementation of Xunit.Abstractions.ITestFrameworkExecutor that supports
    //     execution of unit tests linked against xunit.core.dll, using xunit.execution.dll.
    public class YunitTestFrameworkExecutor : XunitTestFrameworkExecutor
    {
        //
        // 摘要:
        //     Initializes a new instance of the Xunit.Sdk.XunitTestFrameworkExecutor class.
        //
        // 参数:
        //   assemblyName:
        //     Name of the test assembly.
        //
        //   sourceInformationProvider:
        //     The source line number information provider.
        //
        //   diagnosticMessageSink:
        //     The message sink to report diagnostic messages to.
        public YunitTestFrameworkExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink)
            : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
        {
        }

        protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            using (YunitTestAssemblyRunner assemblyRunner = new YunitTestAssemblyRunner(TestAssembly, testCases, base.DiagnosticMessageSink, executionMessageSink, executionOptions))
            {
                await assemblyRunner.RunAsync();
            }
        }
    }
}

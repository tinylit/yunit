using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Yunit.Sdk
{
    public class YunitTestAssemblyRunner : XunitTestAssemblyRunner
    {
        public YunitTestAssemblyRunner(ITestAssembly testAssembly, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions) : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
        {
        }

        protected override Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
        {
            return new YunitTestCollectionRunner(testCollection, testCases, base.DiagnosticMessageSink, messageBus, base.TestCaseOrderer, new ExceptionAggregator(base.Aggregator), cancellationTokenSource).RunAsync();
        }
    }
}

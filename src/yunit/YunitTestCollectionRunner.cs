using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Yunit.Sdk
{
    public class YunitTestCollectionRunner : XunitTestCollectionRunner
    {
        public YunitTestCollectionRunner(ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource) : base(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
        {
        }

        protected override async Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
        {
            var runtimeType = testClass.Class.ToRuntimeType();

            var startupAttribute = runtimeType.GetCustomAttribute<StartupAttribute>();

            if (startupAttribute is null)
            {
                try
                {
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(runtimeType.TypeHandle);
                }
                catch (Exception e)
                {
                    Aggregator.Add(new TypeInitializationException(runtimeType.FullName, e));
                }

                return await base.RunTestClassAsync(testClass, @class, testCases);
            }

            try
            {
                return await RunTestClassCoreAsync(testClass, @class, testCases, startupAttribute);
            }
            catch (Exception ex)
            {
                Aggregator.Add(ex);

                return await base.RunTestClassAsync(testClass, @class, testCases);
            }
        }

        protected virtual async Task<RunSummary> RunTestClassCoreAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases, StartupAttribute startupAttribute)
        {
            var runtimeType = testClass.Class.ToRuntimeType();

            var startupAttributeType = startupAttribute.GetType();

            var startupDriverAttribute = startupAttributeType.GetCustomAttribute<StartupDriverAttribute>();

            IStartupDriver startupDriver = (IStartupDriver)ActivatorUtilities.CreateInstance(new ServiceProvider(runtimeType), startupDriverAttribute.StartupDriverType);

            using (var driver = startupDriver.StartHost(startupAttribute))
            {
                switch (driver)
                {
                    case IHost host:

                        await host.StartAsync()
                            .ConfigureAwait(false);

                        try
                        {
                            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(runtimeType.TypeHandle);
                        }
                        catch (Exception e)
                        {
                            Aggregator.Add(new TypeInitializationException(runtimeType.FullName, e));
                        }

                        try
                        {
                            return await new YunitTestClassRunner(testClass, @class, testCases, base.DiagnosticMessageSink, base.MessageBus, base.TestCaseOrderer, new ExceptionAggregator(base.Aggregator), base.CancellationTokenSource, CollectionFixtureMappings, host.Services)
                                   .RunAsync();
                        }
                        finally
                        {
                            await host.StopAsync()
                                .ConfigureAwait(false);
                        }
                    case IWebHost webHost:

                        await webHost.StartAsync()
                            .ConfigureAwait(false);

                        try
                        {
                            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(runtimeType.TypeHandle);
                        }
                        catch (Exception e)
                        {
                            Aggregator.Add(new TypeInitializationException(runtimeType.FullName, e));
                        }

                        try
                        {
                            return await new YunitTestClassRunner(testClass, @class, testCases, base.DiagnosticMessageSink, base.MessageBus, base.TestCaseOrderer, new ExceptionAggregator(base.Aggregator), base.CancellationTokenSource, CollectionFixtureMappings, webHost.Services)
                                   .RunAsync();
                        }
                        finally
                        {
                            await webHost.StopAsync()
                                .ConfigureAwait(false);
                        }
                    default:
                        throw new InvalidOperationException($"驱动器({startupDriverAttribute.StartupDriverType}).StartHost({startupAttributeType})需返回{typeof(IHost)}或者{typeof(IWebHost)}类型的对象。");
                }
            }
        }

        private class ServiceProvider : IServiceProvider
        {
            private readonly Type testClassType;

            public ServiceProvider(Type testClassType)
            {
                this.testClassType = testClassType;
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(Type))
                {
                    return testClassType;
                }

                if (serviceType == typeof(Type[]))
                {
                    return testClassType.GetConstructors()
                        .SelectMany(x => x.GetParameters())
                        .Where(x => !x.ParameterType.IsSimple())
                        .Select(x => x.ParameterType)
                        .ToArray();
                }

                if (serviceType.IsAssignableFrom(typeof(List<Type>)))
                {
                    return testClassType.GetConstructors()
                        .SelectMany(x => x.GetParameters())
                        .Where(x => !x.ParameterType.IsSimple())
                        .Select(x => x.ParameterType)
                        .ToList();
                }

                return null;
            }
        }
    }
}

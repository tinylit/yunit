using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Yunit.Sdk
{
    public class YunitTestClassRunner : XunitTestClassRunner
    {
        //
        // 摘要:
        //     Initializes a new instance of the Xunit.Sdk.XunitTestClassRunner class.
        //
        // 参数:
        //   testClass:
        //     The test class to be run.
        //
        //   class:
        //     The test class that contains the tests to be run.
        //
        //   testCases:
        //     The test cases to be run.
        //
        //   diagnosticMessageSink:
        //     The message sink used to send diagnostic messages
        //
        //   messageBus:
        //     The message bus to report run status to.
        //
        //   testCaseOrderer:
        //     The test case orderer that will be used to decide how to order the test.
        //
        //   aggregator:
        //     The exception aggregator used to run code and collect exceptions.
        //
        //   cancellationTokenSource:
        //     The task cancellation token source, used to cancel the test run.
        //
        //   collectionFixtureMappings:
        //     The mapping of collection fixture types to fixtures.
        public YunitTestClassRunner(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, IDictionary<Type, object> collectionFixtureMappings, IServiceProvider serviceProvider)
            : base(testClass, @class, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        protected override void CreateClassFixture(Type fixtureType)
        {
            if (fixtureType.IsSimple())
            {
                Aggregator.Run(() =>
                {
                    ClassFixtureMappings[fixtureType] = fixtureType.IsArray
                        ? Array.CreateInstance(fixtureType.GetElementType(), 0)
                        : Activator.CreateInstance(fixtureType);
                });
            }
            else
            {
                Aggregator.Run(() => ClassFixtureMappings[fixtureType] = ActivatorUtilities.GetServiceOrCreateInstance(ServiceProvider, fixtureType));
            }

        }

        protected override object[] CreateTestClassConstructorArguments()
        {
            if (Class.Type.IsAbstract)
            {
                return new object[0];
            }

            ConstructorInfo constructorInfo = SelectTestClassConstructor();

            if (constructorInfo is null)
            {
                return new object[0];
            }

            ParameterInfo[] parameters = constructorInfo.GetParameters();

            object[] array = new object[parameters.Length];

            if (parameters.Length == 0)
            {
                return array;
            }

            List<Tuple<int, ParameterInfo>> list = new List<Tuple<int, ParameterInfo>>();

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameterInfo = parameters[i];

                if (TryGetConstructorArgument(constructorInfo, i, parameterInfo, out object argumentValue))
                {
                    array[i] = argumentValue;
                }
                else if (parameterInfo.HasDefaultValue)
                {
                    array[i] = parameterInfo.DefaultValue;
                }
                else if (parameterInfo.IsOptional)
                {
                    array[i] = parameterInfo.DefaultValue;
                }
                else if (parameterInfo.IsDefined(typeof(ParamArrayAttribute)))
                {
                    array[i] = Array.CreateInstance(parameterInfo.ParameterType, new int[1]);
                }
                else if (parameterInfo.ParameterType.IsSimple())
                {
                    object parameterValue = parameterInfo.ParameterType.IsArray
                        ? Array.CreateInstance(parameterInfo.ParameterType.GetElementType(), 0)
                        : Activator.CreateInstance(parameterInfo.ParameterType);

                    array[i] = parameterValue;
                }
                else
                {
                    var parameterValue = ServiceProvider.GetService(parameterInfo.ParameterType);

                    if (parameterValue is null)
                    {
                        list.Add(new Tuple<int, ParameterInfo>(i, parameterInfo));
                    }
                    else
                    {
                        array[i] = parameterValue;
                    }
                }
            }

            if (list.Count > 0)
            {
                Aggregator.Add(new TestClassException(FormatConstructorArgsMissingMessage(constructorInfo, list)));
            }

            return array;
        }
        protected override ConstructorInfo SelectTestClassConstructor()
        {
            var constructorInfos = base.Class.Type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            if (constructorInfos.Length == 1)
            {
                return constructorInfos[0];
            }

            base.Aggregator.Add(new TestClassException("A test class may only define a single public constructor."));

            return null;
        }
    }
}

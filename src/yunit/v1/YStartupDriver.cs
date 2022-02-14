using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yunit.Sdk
{
    public class YStartupDriver : IStartupDriver
    {
        private readonly Type testClassType;

        public YStartupDriver(Type testClassType)
        {
            this.testClassType = testClassType;
        }

        public IDisposable StartHost(StartupAttribute attribute)
        {
            var provider = new ServiceProvider(testClassType);

            object instance = ActivatorUtilities.CreateInstance(provider, attribute.StartupType);

            var configureBuilder = FindConfigureDelegate(attribute.StartupType);

            var configureServicesBuilder = FindConfigureServicesDelegate(attribute.StartupType);

            var configureContainerBuilder = FindConfigureContainerDelegate(attribute.StartupType);

            Func<IServiceCollection, IServiceProvider> configureServices;

            if (configureContainerBuilder.MethodInfo is null)
            {
                configureServices = configureServicesBuilder.Build(instance);
            }
            else
            {
                var containerType = configureContainerBuilder.GetContainerType();

                var builder = (ConfigureServicesDelegateBuilder)Activator.CreateInstance(
                        typeof(ConfigureServicesDelegateBuilder<>).MakeGenericType(containerType),
                        provider,
                        configureServicesBuilder,
                        configureContainerBuilder,
                        instance);

                configureServices = builder.Build();
            }

            IServiceCollection services = new ServiceCollection();

            var serviceProvider = configureServices(services) ?? services.BuildServiceProvider();

            var configure = configureBuilder.Build(instance);

            configure.Invoke(serviceProvider);

            return new Host(serviceProvider);
        }
        private class Host : IHost
        {
            public Host(IServiceProvider services) => Services = services;

            public IServiceProvider Services { get; }

            public void Dispose()
            {
            }

            public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
        private static ConfigureBuilder FindConfigureDelegate(Type startupType)
        {
            var configureMethod = FindMethod(startupType, "Configure", typeof(void), required: false);

            return new ConfigureBuilder(configureMethod);
        }
        private static ConfigureContainerBuilder FindConfigureContainerDelegate(Type startupType)
        {
            var configureMethod = FindMethod(startupType, "ConfigureContainer", typeof(void), required: false);

            return new ConfigureContainerBuilder(configureMethod);
        }
        private static ConfigureServicesBuilder FindConfigureServicesDelegate(Type startupType)
        {
            var servicesMethod = FindMethod(startupType, "ConfigureServices", typeof(IServiceProvider), required: false)
                    ?? FindMethod(startupType, "ConfigureServices", typeof(void), required: false);

            return new ConfigureServicesBuilder(servicesMethod) { StartupServiceFilters = BuildStartupServicesFilterPipeline };
        }
        private static Func<IServiceCollection, IServiceProvider> BuildStartupServicesFilterPipeline(Func<IServiceCollection, IServiceProvider> startup) => startup;
        private static MethodInfo FindMethod(Type startupType, string methodName, Type returnType = null, bool required = true)
        {
            var methods = startupType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            var selectedMethods = methods
                .Where(method => method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (selectedMethods.Count > 1)
            {
                throw new InvalidOperationException($"Having multiple overloads of method '{methodName}' is not supported.");
            }

            var methodInfo = selectedMethods.FirstOrDefault();

            if (methodInfo == null)
            {
                if (required)
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture,
                        "A public method named '{0}' could not be found in the '{1}' type.",
                        methodName,
                        startupType.FullName));

                }

                return null;
            }

            if (returnType != null && methodInfo.ReturnType != returnType)
            {
                if (required)
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture,
                        "The '{0}' method in the type '{1}' must have a return type of '{2}'.",
                        methodInfo.Name,
                        startupType.FullName,
                        returnType.Name));
                }

                return null;
            }

            return methodInfo;
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
        private class ConfigureContainerBuilder
        {
            public MethodInfo MethodInfo
            {
                get;
            }

            public Func<Action<object>, Action<object>> ConfigureContainerFilters
            {
                get;
                set;
            }

            public ConfigureContainerBuilder(MethodInfo configureContainerMethod)
            {
                MethodInfo = configureContainerMethod;
            }

            public Action<object> Build(object instance)
            {
                return delegate (object container)
                {
                    Invoke(instance, container);
                };
            }

            public Type GetContainerType()
            {
                ParameterInfo[] parameters = MethodInfo.GetParameters();
                if (parameters.Length != 1)
                {
                    throw new InvalidOperationException("The " + MethodInfo.Name + " method must take only one parameter.");
                }

                return parameters[0].ParameterType;
            }

            private void Invoke(object instance, object container)
            {
                ConfigureContainerFilters(StartupConfigureContainer)(container);
                void StartupConfigureContainer(object containerBuilder)
                {
                    InvokeCore(instance, containerBuilder);
                }
            }

            private void InvokeCore(object instance, object container)
            {
                if (!(MethodInfo == null))
                {
                    object[] parameters = new object[1]
                    {
                    container
                    };
                    MethodInfo.Invoke(instance, parameters);
                }
            }
        }
        public class ConfigureBuilder
        {
            public MethodInfo MethodInfo
            {
                get;
            }

            public ConfigureBuilder(MethodInfo configure)
            {
                MethodInfo = configure;
            }

            public Action<IServiceProvider> Build(object instance)
            {
                return delegate (IServiceProvider builder)
                {
                    Invoke(instance, builder);
                };
            }

            private void Invoke(object instance, IServiceProvider serviceProvider)
            {
                if (MethodInfo is null)
                {
                    return;
                }

                using (IServiceScope serviceScope = ServiceProviderServiceExtensions.CreateScope(serviceProvider))
                {
                    ParameterInfo[] parameters = MethodInfo.GetParameters();
                    object[] array = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        ParameterInfo parameterInfo = parameters[i];

                        try
                        {
                            array[i] = ServiceProviderServiceExtensions.GetRequiredService(serviceScope.ServiceProvider, parameterInfo.ParameterType);
                        }
                        catch (Exception innerException)
                        {
                            throw new Exception($"Could not resolve a service of type '{parameterInfo.ParameterType.FullName}' for the parameter '{parameterInfo.Name}' of method '{MethodInfo.Name}' on type '{MethodInfo.DeclaringType.FullName}'.", innerException);
                        }
                    }

                    MethodInfo.Invoke(MethodInfo.IsStatic ? null : instance, array);
                }
            }
        }
        private class ConfigureServicesBuilder
        {
            public MethodInfo MethodInfo
            {
                get;
            }

            public Func<Func<IServiceCollection, IServiceProvider>, Func<IServiceCollection, IServiceProvider>> StartupServiceFilters
            {
                get;
                set;
            }

            public ConfigureServicesBuilder(MethodInfo configureServices)
            {
                MethodInfo = configureServices;
            }

            public Func<IServiceCollection, IServiceProvider> Build(object instance)
            {
                return (IServiceCollection services) => Invoke(instance, services);
            }

            private IServiceProvider Invoke(object instance, IServiceCollection services)
            {
                return StartupServiceFilters.Invoke(Startup).Invoke(services);

                IServiceProvider Startup(IServiceCollection serviceCollection)
                {
                    return InvokeCore(instance, serviceCollection);
                }
            }

            private IServiceProvider InvokeCore(object instance, IServiceCollection services)
            {
                if (MethodInfo == null)
                {
                    return null;
                }

                ParameterInfo[] parameters = MethodInfo.GetParameters();
                if (parameters.Length > 1 || parameters.Any((ParameterInfo p) => p.ParameterType != typeof(IServiceCollection)))
                {
                    throw new InvalidOperationException("The ConfigureServices method must either be parameterless or take only one parameter of type IServiceCollection.");
                }

                object[] array = new object[parameters.Length];
                if (parameters.Length != 0)
                {
                    array[0] = services;
                }

                return MethodInfo.Invoke(instance, array) as IServiceProvider;
            }
        }
        private abstract class ConfigureServicesDelegateBuilder
        {
            public abstract Func<IServiceCollection, IServiceProvider> Build();
        }
        private class ConfigureServicesDelegateBuilder<TContainerBuilder> : ConfigureServicesDelegateBuilder
        {
            public ConfigureServicesDelegateBuilder(
                IServiceProvider hostingServiceProvider,
                ConfigureServicesBuilder configureServicesBuilder,
                ConfigureContainerBuilder configureContainerBuilder,
                object instance)
            {
                HostingServiceProvider = hostingServiceProvider;
                ConfigureServicesBuilder = configureServicesBuilder;
                ConfigureContainerBuilder = configureContainerBuilder;
                Instance = instance;
            }

            public IServiceProvider HostingServiceProvider { get; }
            public ConfigureServicesBuilder ConfigureServicesBuilder { get; }
            public ConfigureContainerBuilder ConfigureContainerBuilder { get; }
            public object Instance { get; }
            public override Func<IServiceCollection, IServiceProvider> Build()
            {
                ConfigureServicesBuilder.StartupServiceFilters = BuildStartupServicesFilterPipeline;
                var configureServicesCallback = ConfigureServicesBuilder.Build(Instance);

                ConfigureContainerBuilder.ConfigureContainerFilters = ConfigureContainerPipeline;
                var configureContainerCallback = ConfigureContainerBuilder.Build(Instance);

                return ConfigureServices(configureServicesCallback, configureContainerCallback);

                Action<object> ConfigureContainerPipeline(Action<object> action)
                {
                    return Target;

                    // The ConfigureContainer pipeline needs an Action<TContainerBuilder> as source, so we just adapt the
                    // signature with this function.
                    void Source(TContainerBuilder containerBuilder) =>
                        action(containerBuilder);

                    // The ConfigureContainerBuilder.ConfigureContainerFilters expects an Action<object> as value, but our pipeline
                    // produces an Action<TContainerBuilder> given a source, so we wrap it on an Action<object> that internally casts
                    // the object containerBuilder to TContainerBuilder to match the expected signature of our ConfigureContainer pipeline.
                    void Target(object containerBuilder) =>
                        BuildStartupConfigureContainerFiltersPipeline(Source)((TContainerBuilder)containerBuilder);
                }
            }

            Func<IServiceCollection, IServiceProvider> ConfigureServices(
                Func<IServiceCollection, IServiceProvider> configureServicesCallback,
                Action<object> configureContainerCallback)
            {
                return ConfigureServicesWithContainerConfiguration;

                IServiceProvider ConfigureServicesWithContainerConfiguration(IServiceCollection services)
                {
                    // Call ConfigureServices, if that returned an IServiceProvider, we're done
                    var applicationServiceProvider = configureServicesCallback.Invoke(services);

                    if (applicationServiceProvider != null)
                    {
                        return applicationServiceProvider;
                    }

                    // If there's a ConfigureContainer method
                    if (ConfigureContainerBuilder.MethodInfo != null)
                    {
                        var serviceProviderFactory = HostingServiceProvider.GetRequiredService<IServiceProviderFactory<TContainerBuilder>>();
                        var builder = serviceProviderFactory.CreateBuilder(services);
                        configureContainerCallback(builder);
                        applicationServiceProvider = serviceProviderFactory.CreateServiceProvider(builder);
                    }
                    else
                    {
                        // Get the default factory
                        var serviceProviderFactory = HostingServiceProvider.GetRequiredService<IServiceProviderFactory<IServiceCollection>>();
                        var builder = serviceProviderFactory.CreateBuilder(services);
                        applicationServiceProvider = serviceProviderFactory.CreateServiceProvider(builder);
                    }

                    return applicationServiceProvider ?? services.BuildServiceProvider();
                }
            }

            private Func<IServiceCollection, IServiceProvider> BuildStartupServicesFilterPipeline(Func<IServiceCollection, IServiceProvider> startup)
            {
                return RunPipeline;

                IServiceProvider RunPipeline(IServiceCollection services)
                {
                    var filters = HostingServiceProvider.GetRequiredService<IEnumerable<IStartupConfigureServicesFilter>>().ToArray();

                    // If there are no filters just run startup (makes IServiceProvider ConfigureServices(IServiceCollection services) work.
                    if (filters.Length == 0)
                    {
                        return startup(services);
                    }

                    Action<IServiceCollection> pipeline = InvokeStartup;
                    for (int i = filters.Length - 1; i >= 0; i--)
                    {
                        pipeline = filters[i].ConfigureServices(pipeline);
                    }

                    pipeline(services);

                    // We return null so that the host here builds the container (same result as void ConfigureServices(IServiceCollection services);
                    return null;

                    void InvokeStartup(IServiceCollection serviceCollection)
                    {
                        var result = startup(serviceCollection);

                        if (filters.Length > 0 && result != null)
                        {
                            var message = $"A ConfigureServices method that returns an {nameof(IServiceProvider)} is " +
                                $"not compatible with the use of one or more {nameof(IStartupConfigureServicesFilter)}. " +
                                $"Use a void returning ConfigureServices method instead or a ConfigureContainer method.";
                            throw new InvalidOperationException(message);
                        };
                    }
                }
            }

            private Action<TContainerBuilder> BuildStartupConfigureContainerFiltersPipeline(Action<TContainerBuilder> configureContainer)
            {
                return RunPipeline;

                void RunPipeline(TContainerBuilder containerBuilder)
                {
                    var filters = HostingServiceProvider.GetRequiredService<IEnumerable<IStartupConfigureContainerFilter<TContainerBuilder>>>();

                    Action<TContainerBuilder> pipeline = InvokeConfigureContainer;
                    foreach (var filter in filters.Reverse())
                    {
                        pipeline = filter.ConfigureContainer(pipeline);
                    }

                    pipeline(containerBuilder);

                    void InvokeConfigureContainer(TContainerBuilder builder) => configureContainer(builder);
                }
            }
        }
    }
}

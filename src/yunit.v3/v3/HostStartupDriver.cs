using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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
    public class HostStartupDriver : IStartupDriver
    {
        public IDisposable StartHost(StartupAttribute attribute) => StartupLoader.CreateHost(attribute.StartupType);

        private static class StartupLoader
        {
            public static IHost CreateHost(Type startupType)
            {
                var hostBuilder = Host.CreateDefaultBuilder();

                var configureHost = Configure(hostBuilder);

                hostBuilder.ConfigureServices((context, services) =>
                {
                    IHostEnvironment environment, hostingEnvironment = environment = context.HostingEnvironment;

                    var hostingEnvironmentType = Type.GetType("Microsoft.AspNetCore.Hosting.HostingEnvironment, Microsoft.AspNetCore.Hosting", false, true);

                    if (hostingEnvironmentType is null || hostingEnvironmentType.IsAssignableFrom(environment.GetType()))
                    {
                        services.AddSingleton(environment);

                        context.Properties[typeof(IHostEnvironment)] = environment;
                    }
                    else
                    {
                        hostingEnvironment = (IHostEnvironment)Activator.CreateInstance(hostingEnvironmentType, true);

                        hostingEnvironment.EnvironmentName = environment.EnvironmentName;
                        hostingEnvironment.ApplicationName = environment.ApplicationName;
                        hostingEnvironment.ContentRootPath = environment.ContentRootPath;
                        hostingEnvironment.ContentRootFileProvider = environment.ContentRootFileProvider;

                        foreach (var propertyInfo in hostingEnvironmentType.GetProperties())
                        {
                            if (propertyInfo.PropertyType == typeof(IFileProvider))
                            {
                                propertyInfo.SetValue(hostingEnvironment, environment.ContentRootFileProvider, null);
                            }
                            else if (propertyInfo.Name == "WebRootPath")
                            {
                                propertyInfo.SetValue(hostingEnvironment, environment.ContentRootPath, null);
                            }
                        }

                        foreach (var interfaceType in hostingEnvironmentType.GetInterfaces())
                        {
                            services.AddSingleton(interfaceType, hostingEnvironment);
                        }

                        context.Properties[typeof(IHostEnvironment)] = hostingEnvironment;
                    }

                    var hostServiceProvider = new HostServiceProvider(context.Configuration, hostingEnvironment);

                    Func<IServiceCollection, IServiceProvider> configureServices;

                    object instance = ActivatorUtilities.CreateInstance(hostServiceProvider, startupType);

                    var configureHostMethod = FindMethod(startupType, "Configure{0}Host", environment.EnvironmentName, typeof(void), required: false);

                    if (configureHostMethod != null)
                    {
                        configureHost.ConfigureHost(builder =>
                        {
                            var parameters = configureHostMethod.GetParameters();

                            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(IWebHostBuilder))
                                throw new InvalidOperationException($"The '{configureHostMethod.Name}' method of startup type '{startupType.FullName}' must have the single 'IHostBuilder' parameter.");

                            configureHostMethod.Invoke(configureHostMethod.IsStatic ? null : instance, new object[] { builder });
                        });
                    }

                    var configureServicesBuilder = FindConfigureServicesDelegate(startupType, environment.EnvironmentName);

                    var configureContainerBuilder = FindConfigureContainerDelegate(startupType, environment.EnvironmentName);

                    if (configureContainerBuilder.MethodInfo is null)
                    {
                        configureServices = configureServicesBuilder.Build(instance);
                    }
                    else
                    {
                        var containerType = configureContainerBuilder.GetContainerType();

                        var builder = (ConfigureServicesDelegateBuilder)Activator.CreateInstance(
                                typeof(ConfigureServicesDelegateBuilder<>).MakeGenericType(containerType),
                                hostServiceProvider,
                                configureServicesBuilder,
                                configureContainerBuilder,
                                instance);

                        configureServices = builder.Build();
                    }

                    configureServices(services);

                    var configureMethod = FindMethod(startupType, "Configure{0}", environment.EnvironmentName, typeof(void), required: false);

                    if (configureMethod is null)
                    {
                        return;
                    }

                    services.AddSingleton<IServer, TestServer>();

                    var configureBuilder = new ConfigureBuilder(configureMethod);

                    configureHost.Configure(configureBuilder.Build(instance));

                }).ConfigureServices(services =>
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    string name = "Microsoft.AspNetCore.Mvc.ControllerBase, Microsoft.AspNetCore.Mvc.Core";

                    var baseControllerType = Type.GetType(name, false, true);

                    if (baseControllerType is null)
                    {
                        return;
                    }

                    foreach (var controllerType in assemblies.SelectMany(x => x.GetTypes())
                        .Where(x => !x.IsInterface && !x.IsAbstract)
                        .Where(baseControllerType.IsAssignableFrom))
                    {
                        services.AddScoped(controllerType, controllerType);
                    }
                });

                return hostBuilder.Build();
            }

            private static ConfigureHostBuilder Configure(IHostBuilder hostBuilder)
            {
                var hostBuilderExtensionsType = Type.GetType("Microsoft.Extensions.Hosting.GenericHostWebHostBuilderExtensions, Microsoft.AspNetCore.Hosting", false, true);

                if (hostBuilderExtensionsType is null)
                {
                    return new ConfigureHostBuilder(false);
                }

                var configureWebHost = hostBuilderExtensionsType.GetMethod("ConfigureWebHost", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(IHostBuilder), typeof(Action<IWebHostBuilder>) }, null);

                if (configureWebHost is null)
                {
                    return new ConfigureHostBuilder(false);
                }

                ConfigureHostBuilder configureHost = new ConfigureHostBuilder();

                configureWebHost.Invoke(null, new object[] { hostBuilder, new Action<IWebHostBuilder>(configureHost.Build) });

                return configureHost;
            }

            public class TestServer : IServer, IDisposable
            {
                public TestServer() : this(new FeatureCollection())
                {
                }

                public TestServer(IFeatureCollection featureCollection) => Features = featureCollection;

                public IFeatureCollection Features { get; }

                public void Dispose()
                {
                }

                public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken) => Task.CompletedTask;

                public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            }

            private class HostServiceProvider : IServiceProvider
            {
                private readonly IConfiguration configuration;
                private readonly IHostEnvironment environment;

                public HostServiceProvider(IConfiguration configuration, IHostEnvironment environment)
                {
                    this.configuration = configuration;
                    this.environment = environment;
                }

                public object GetService(Type serviceType)
                {
                    if (serviceType == typeof(IConfiguration))
                    {
                        return configuration;
                    }

                    if (serviceType == typeof(IHostEnvironment)
                        || serviceType == typeof(Microsoft.AspNetCore.Hosting.IHostingEnvironment))
                    {
                        return environment;
                    }

                    return null;
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
                    return StartupServiceFilters(Startup)(services);
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

            public class ConfigureHostBuilder
            {
                private readonly List<Action<IApplicationBuilder>> configures;
                private readonly List<Action<IWebHostBuilder>> configureHosts;

                public ConfigureHostBuilder(bool isValid = true)
                {
                    IsValid = isValid;
                    configures = new List<Action<IApplicationBuilder>>(1);

                    if (isValid)
                    {
                        configureHosts = new List<Action<IWebHostBuilder>>(1);
                    }
                }

                /// <summary>
                /// 有效。
                /// </summary>
                public bool IsValid { get; }

                public void Configure(Action<IApplicationBuilder> configure)
                {
                    if (configure is null)
                    {
                        throw new ArgumentNullException(nameof(configure));
                    }

                    configures.Add(configure);
                }

                public void ConfigureHost(Action<IWebHostBuilder> configureHost)
                {
                    if (configureHost is null)
                    {
                        throw new ArgumentNullException(nameof(configureHost));
                    }

                    if (IsValid)
                    {
                        configureHosts.Add(configureHost);
                    }
                }

                public void Build(IWebHostBuilder builder)
                {
                    if (IsValid)
                    {
                        foreach (var configureHost in configureHosts)
                        {
                            configureHost.Invoke(builder);
                        }

                        builder.Configure(InvokeCore);
                    }
                }

                private void InvokeCore(IApplicationBuilder builder)
                {
                    foreach (var configure in configures)
                    {
                        configure.Invoke(builder);
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

                public Action<IApplicationBuilder> Build(object instance)
                {
                    return delegate (IApplicationBuilder builder)
                    {
                        Invoke(instance, builder);
                    };
                }

                private void Invoke(object instance, IApplicationBuilder builder)
                {
                    using (IServiceScope serviceScope = ServiceProviderServiceExtensions.CreateScope(builder.ApplicationServices))
                    {
                        IServiceProvider serviceProvider = serviceScope.ServiceProvider;
                        ParameterInfo[] parameters = MethodInfo.GetParameters();
                        object[] array = new object[parameters.Length];
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            ParameterInfo parameterInfo = parameters[i];

                            if (parameterInfo.ParameterType == typeof(IApplicationBuilder))
                            {
                                array[i] = builder;

                                continue;
                            }

                            try
                            {
                                array[i] = ServiceProviderServiceExtensions.GetRequiredService(serviceProvider, parameterInfo.ParameterType);
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

            private static ConfigureContainerBuilder FindConfigureContainerDelegate(Type startupType, string environmentName)
            {
                var configureMethod = FindMethod(startupType, "Configure{0}Container", environmentName, typeof(void), required: false);

                return new ConfigureContainerBuilder(configureMethod);
            }

            private static ConfigureServicesBuilder FindConfigureServicesDelegate(Type startupType, string environmentName)
            {
                var servicesMethod = FindMethod(startupType, "Configure{0}Services", environmentName, typeof(IServiceProvider), required: false)
                    ?? FindMethod(startupType, "Configure{0}Services", environmentName, typeof(void), required: false);

                return new ConfigureServicesBuilder(servicesMethod) { StartupServiceFilters = BuildStartupServicesFilterPipeline };
            }

            private static Func<IServiceCollection, IServiceProvider> BuildStartupServicesFilterPipeline(Func<IServiceCollection, IServiceProvider> startup) => startup;

            private static MethodInfo FindMethod(Type startupType, string methodName, string environmentName, Type returnType = null, bool required = true)
            {
                var methodNameWithEnv = string.Format(CultureInfo.InvariantCulture, methodName, environmentName);
                var methodNameWithNoEnv = string.Format(CultureInfo.InvariantCulture, methodName, string.Empty);

                var methods = startupType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                var selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithEnv, StringComparison.OrdinalIgnoreCase)).ToList();
                if (selectedMethods.Count > 1)
                {
                    throw new InvalidOperationException($"Having multiple overloads of method '{methodNameWithEnv}' is not supported.");
                }
                if (selectedMethods.Count == 0)
                {
                    selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithNoEnv, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (selectedMethods.Count > 1)
                    {
                        throw new InvalidOperationException($"Having multiple overloads of method '{methodNameWithNoEnv}' is not supported.");
                    }
                }

                var methodInfo = selectedMethods.FirstOrDefault();
                if (methodInfo == null)
                {
                    if (required)
                    {
                        throw new InvalidOperationException(string.Format(
                            CultureInfo.CurrentCulture,
                            "A public method named '{0}' or '{1}' could not be found in the '{2}' type.",
                            methodNameWithEnv,
                            methodNameWithNoEnv,
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
        }
    }
}

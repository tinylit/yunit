using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Yunit.Sdk
{
    /// <summary>
    /// 默认启动类。
    /// </summary>
    public class YStartup
    {
        private readonly Type[] parameterTypes;

        public YStartup(Type[] parameterTypes)
        {
            this.parameterTypes = parameterTypes ?? Type.EmptyTypes;
        }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            var assemblys = AppDomain.CurrentDomain.GetAssemblies();

            var assemblyTypes = assemblys
                .SelectMany(x => x.GetTypes())
                .Where(x => x.IsClass || x.IsInterface)
                .ToList();

            int maxDepth = 5;

            services.AddLogging();

            foreach (var parameterType in parameterTypes)
            {
                if (Di(services, parameterType, assemblyTypes, 0, maxDepth, ServiceLifetime.Scoped))
                {
                    continue;
                }

                throw new TypeLoadException($"Parameter '{parameterType.FullName}' cannot be created and the current maximum dependency injection depth is {maxDepth}.");
            }
        }

        private static bool Di(IServiceCollection services, Type serviceType, List<Type> assemblyTypes, int depth, int maxDepth, ServiceLifetime lifetime)
        {
            if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory))
            {
                return true;
            }

            bool isSingle = true;

            //? 集合获取。
            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                isSingle = false;

                serviceType = serviceType.GetGenericArguments()[0];
            }

            if (services.Any(x => x.ServiceType == serviceType)) //? 人为注入时，不再自动注入。
            {
                return true;
            }

            if (serviceType.IsGenericType)
            {
                var typeDefinition = serviceType.GetGenericTypeDefinition();

                if (services.Any(x => x.ServiceType == typeDefinition)) //? 人为注入时，不再自动注入。
                {
                    return true;
                }
            }

            var implementationTypes = (serviceType.IsInterface || serviceType.IsAbstract)
                ? assemblyTypes
                    .Where(y => y.IsPublic && y.IsClass && !y.IsAbstract && serviceType.IsAssignableFrom(y))
                    .ToList()
                : new List<Type> { serviceType };

            bool flag = false;

            foreach (var implementationType in implementationTypes)
            {
                foreach (var constructorInfo in implementationType.GetConstructors())
                {
                    if (!constructorInfo.IsPublic)
                    {
                        continue;
                    }

                    flag = true;

                    foreach (var parameterInfo in constructorInfo.GetParameters())
                    {
                        if (parameterInfo.IsOptional)
                        {
                            continue;
                        }

                        if (serviceType.IsAssignableFrom(parameterInfo.ParameterType)) //? 避免循环依赖。
                        {
                            flag = false;

                            break;
                        }

                        if (Di(services, parameterInfo.ParameterType, assemblyTypes, depth + 1, maxDepth, lifetime))
                        {
                            continue;
                        }

                        flag = false;

                        break;
                    }

                    if (flag)
                    {
                        break;
                    }
                }

                if (flag)
                {
                    switch (lifetime)
                    {
                        case ServiceLifetime.Singleton:
                            services.AddSingleton(serviceType, implementationType);
                            break;
                        case ServiceLifetime.Scoped:
                            services.AddScoped(serviceType, implementationType);
                            break;
                        case ServiceLifetime.Transient:
                        default:
                            services.AddTransient(serviceType, implementationType);
                            break;
                    }

                    if (isSingle) //? 注入一个支持。
                    {
                        break;
                    }
                }
            }

            if (!flag)
            {
                if (serviceType.IsGenericType)
                {
                    var typeDefinition = serviceType.GetGenericTypeDefinition();

                    if (services.Any(x => x.ServiceType == typeDefinition))
                    {
                        return true;
                    }
                }
            }

            return flag;
        }
    }
}

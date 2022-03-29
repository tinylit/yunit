using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

            int maxDepth = 10;

            services.AddLogging();

            List<Type> dependencies = new List<Type>(maxDepth * 2 + 3);

            foreach (var parameterType in parameterTypes)
            {
                if (Di(services, parameterType, assemblyTypes, 0, maxDepth, ServiceLifetime.Scoped, dependencies))
                {
                    dependencies.Clear();

                    continue;
                }

                var sb = new StringBuilder();

                sb.Append("Parameter '")
                    .Append(parameterType.Name)
                    .Append("' cannot be created and the current maximum dependency injection depth is ")
                    .Append(maxDepth)
                    .AppendLine(".")
                    .AppendLine("Dependency details are as follows:");

                for (int i = 0, len = dependencies.Count - 1; i < len; i += 2)
                {
                    Type serviceType = dependencies[i];

                    Type implementationType = dependencies[i + 1];

                    if (i > 0)
                    {
                        sb.Append(" => ")
                            .Append(Environment.NewLine);

                        for (int j = 0; j < i; j += 2)
                        {
                            sb.Append("  ");
                        }
                    }

                    if (serviceType == implementationType)
                    {
                        sb.Append(serviceType.Name);
                    }
                    else
                    {
                        sb.Append('{')
                            .Append(serviceType.Name)
                            .Append('=')
                            .Append(implementationType.Name)
                            .Append('}');
                    }
                }

                throw new TypeLoadException(sb.Append('.').ToString());
            }
        }

        private static bool Di(IServiceCollection services, Type serviceType, List<Type> assemblyTypes, int depth, int maxDepth, ServiceLifetime lifetime, List<Type> dependencies)
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

            int startIndex = dependencies.Count;

            foreach (var implementationType in implementationTypes)
            {
                dependencies.Add(implementationType);

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

                        dependencies.Add(parameterInfo.ParameterType);

                        if (serviceType.IsAssignableFrom(parameterInfo.ParameterType)) //? 避免循环依赖。
                        {
                            flag = false;

                            break;
                        }

                        if (Di(services, parameterInfo.ParameterType, assemblyTypes, depth + 1, maxDepth, lifetime, dependencies))
                        {
                            dependencies.RemoveRange(startIndex + 1, dependencies.Count - startIndex - 1);

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

                    if (implementationTypes.Count > 0)
                    {
                        return false;
                    }

                    foreach (var y in assemblyTypes)
                    {
                        if (y.IsInterface || y.IsAbstract)
                        {
                            continue;
                        }

                        if (!y.IsPublic || !y.IsGenericType)
                        {
                            continue;
                        }

                        foreach (var z in y.GetInterfaces())
                        {
                            if (z.IsGenericType && z.GetGenericTypeDefinition() == typeDefinition)
                            {
                                switch (lifetime)
                                {
                                    case ServiceLifetime.Singleton:
                                        services.AddSingleton(typeDefinition, y.GetGenericTypeDefinition());
                                        break;
                                    case ServiceLifetime.Scoped:
                                        services.AddScoped(typeDefinition, y.GetGenericTypeDefinition());
                                        break;
                                    case ServiceLifetime.Transient:
                                    default:
                                        services.AddTransient(typeDefinition, y.GetGenericTypeDefinition());
                                        break;
                                }

                                return true;
                            }
                        }
                    }
                }
            }

            return flag;
        }
    }
}

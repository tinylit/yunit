using System;
using System.Collections.Generic;
using System.Text;

namespace Yunit.Sdk
{
    public static class TypeExtentions
    {
        private static readonly HashSet<Type> _simpleTypes = new HashSet<Type>
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(bool),
            typeof(string),
            typeof(char),
            typeof(Guid),
            typeof(TimeSpan),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(byte[])
        };

        public static bool IsSimple(this Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return type.IsEnum || _simpleTypes.Contains(type);
        }
    }
}

using System;
using System.Collections.Generic;

namespace Match3.Core
{
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T service)
        {
            _services[typeof(T)] = service;
        }

        public static T Get<T>()
        {
            if (_services.TryGetValue(typeof(T), out object service))
                return (T)service;
            throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
        }

        public static bool TryGet<T>(out T service)
        {
            if (_services.TryGetValue(typeof(T), out object obj))
            {
                service = (T)obj;
                return true;
            }
            service = default;
            return false;
        }

        public static void Clear()
        {
            _services.Clear();
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class DIContainer : BaseManager<DIContainer>
{
    private DIContainer() { }

    private Dictionary<Type, object> _services = new();
    private Dictionary<Type, Type> _implementations = new();
    public void Register<T>(T instance)
    {
        _services[typeof(T)] = instance;
    }
    public void Register<TInterface, TImplementation>() where TImplementation: TInterface
    {
        _implementations[typeof(TInterface)] = typeof(TImplementation);
    }
    public T Resolve<T>() where T:class
    {
        if (_services.TryGetValue(typeof(T), out object service)) return service as T;
        throw new Exception($"服务 {typeof(T).Name} 未注册");
    }
    public object Resolve(Type serviceType)
    {
        if(_services.TryGetValue(serviceType,out object existingInstance))return existingInstance;

        Type implementationType = serviceType;
        if(serviceType.IsInterface && _implementations.TryGetValue(serviceType,out Type registeredType))
        {
            implementationType = registeredType;
        }
        else if (serviceType.IsInterface)
        {
            throw new Exception($"服务 {serviceType.Name} 未注册类型映射");
        }

        ConstructorInfo constructor = implementationType.GetConstructors()[0];
        ParameterInfo[] parameters = constructor.GetParameters();

        object[] dependencies = new object[parameters.Length];
        for(int i = 0; i < parameters.Length; i++)
        {
            Type dependencyType = parameters[i].ParameterType;
            dependencies[i] = Resolve(dependencyType);
        }
        
        object instance = constructor.Invoke(dependencies);
        _services[serviceType] = instance;

        return instance;
    }
}

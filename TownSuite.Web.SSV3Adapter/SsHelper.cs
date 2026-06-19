using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using TownSuite.Web.SSV3Adapter.Interfaces;

namespace TownSuite.Web.SSV3Adapter;

internal class SsHelper
{
    // These caches are static (shared across all adapter instances/configurations in the
    // process). To avoid one adapter's resolution leaking onto another adapter's route, every
    // cache key is prefixed with a signature of the owning options (route + service base types
    // + searched assemblies). See GetOptionsSignature.
    private static readonly ConcurrentDictionary<string, (Type Service, MethodInfo Method, Type DtoType)> ServiceMap =
        new();


    private static readonly ConcurrentDictionary<string,
            ConcurrentDictionary<Type, (Type Service, MethodInfo Method, Type DtoType)>>
        SwaggerServiceMap
            = new();

    private readonly ServiceStackV3AdapterOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public SsHelper(ServiceStackV3AdapterOptions options,
        IServiceProvider serviceProvider)
    {
        _options = options;
        _serviceProvider = serviceProvider;
    }

    public async Task<object> ConstructServiceObjectAsync(Type theService)
    {
        object instance;

        // use constructor with most parameters
        var ctors = theService.GetConstructors();
        // assuming class A has only one constructor
        var ctor = ctors
            .Where(ConstructorAvailable)
            .OrderByDescending(p => p.GetParameters().Count())
            .FirstOrDefault();

        if (ctor.GetParameters().Count() == 0)
        {
            // default contructor
            instance = Activator.CreateInstance(theService);
            return instance;
        }

        var ctorParameters = await InitalizeParameters(ctor);
        instance = ctor.Invoke(ctorParameters.ToArray());
        return instance;
    }

    private async Task<List<object>> InitalizeParameters(ConstructorInfo? ctor)
    {
        var ctorParameters = new List<object>();
        foreach (var param in ctor.GetParameters())
        {
            var parameter = _serviceProvider.GetService(param.ParameterType) 
                ?? throw new NotImplementedException($"{param.ParameterType} not found");

            if (_options.CustomCallBack != null) await _options.CustomCallBack((CustomCall.Parameter, parameter, null));

            ctorParameters.Add(parameter);
        }
        return ctorParameters;
    }

    public (Type Service, MethodInfo Method, Type DtoType)?
        GetService(string requestName)
    {
        // Cache key is scoped to this adapter's configuration so a service resolved under one
        // adapter is never served on another adapter's route.
        var cacheKey = $"{GetOptionsSignature(_options)}|{requestName}";
        if (ServiceMap.TryGetValue(cacheKey, out var cached)) return cached;

        foreach (var asm in _options.SearchAssemblies)
        {
            var typeInfo = asm.GetTypes().Where(p => IsServiceType(p)).OrderBy(p => p.Name);
            foreach (var service in typeInfo)
            {
                var methodInfo = GetMethod(requestName, service);
                if (methodInfo.method != null)
                {
                    var entry = (service, methodInfo.method, methodInfo.dtoType);
                    ServiceMap[cacheKey] = entry;
                    return entry;
                }
            }

            // continue on and try the next dll
        }

        return null;
    }

    /// <summary>
    ///     Builds a stable signature of the supplied options so static caches can be partitioned
    ///     per adapter configuration. Includes the route, the allowed service base types, and the
    ///     searched assemblies - the inputs that determine which services an adapter may resolve.
    /// </summary>
    internal static string GetOptionsSignature(ServiceStackV3AdapterOptions options)
    {
        var assemblies = string.Join(",",
            (options.SearchAssemblies ?? Array.Empty<Assembly>())
            .Select(a => a.FullName).OrderBy(x => x, StringComparer.Ordinal));
        var serviceTypes = string.Join(",",
            (options.ServiceTypes ?? Array.Empty<Type>())
            .Select(t => t.FullName).OrderBy(x => x, StringComparer.Ordinal));
        return $"{options.RoutePath}|{assemblies}|{serviceTypes}";
    }

    public ConcurrentDictionary<Type, (Type Service, MethodInfo Method, Type DtoType)>
        GetAllServices()
    {
        var signature = GetOptionsSignature(_options);
        if (SwaggerServiceMap.TryGetValue(signature, out var existing) && existing.Any())
            return existing;

        var map = SwaggerServiceMap.GetOrAdd(signature,
            _ => new ConcurrentDictionary<Type, (Type Service, MethodInfo Method, Type DtoType)>());

        var types = PermissiveLoadAssemblies();

        var typeInfo = types.Where(p => IsServiceType(p)).OrderBy(p => p.Name);
        foreach (var service in typeInfo)
        {
            var methodInfo = GetMethod("", service);
            if (methodInfo.method != null)
                // key, value, func<TKey, TValue, TValue>
                map.AddOrUpdate(methodInfo.method.DeclaringType,
                    (service, methodInfo.method, methodInfo.dtoType),
                    (s, m) => { return (service, methodInfo.method, methodInfo.dtoType); });
        }

        // continue on and try the next dll
        return map;
    }

    private List<Type> PermissiveLoadAssemblies()
    {
        List<Type> types = new List<Type>();

        int assemblyCount = _options.SearchAssemblies.Length;

        for (int i = 0; i < assemblyCount; i++)
        {
            try
            {
                types.AddRange(_options.SearchAssemblies[i].GetTypes());
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Handle assemblies that cannot load all types
                foreach (Type theType in ex.Types)
                {
                    try
                    {
                        types.Add(theType);
                    }
                    catch (Exception)
                    {
                        // Type not in this assembly - reference to elsewhere ignored
                    }
                }
            }
        }

        return types;
    }


    public static bool IsAsyncMethod(MethodInfo method)
    {
        // see https://stackoverflow.com/questions/20350397/how-can-i-tell-if-a-c-sharp-method-is-async-await-via-reflection


        var attType = typeof(AsyncStateMachineAttribute);

        // Obtain the custom attribute for the method. 
        // The value returned contains the StateMachineType property. 
        // Null is returned if the attribute isn't present for the method. 
        var attrib = (AsyncStateMachineAttribute)method.GetCustomAttribute(attType);

        return attrib != null;
    }


    public bool IsServiceType(Type instance)
    {
        foreach (var item in _options.ServiceTypes)
        {
            if (instance == null)
                continue;
            if (instance.BaseType == item)
                return true;
            else if (instance.IsSubclassOf(item))
                return true;
            else if (instance.IsAssignableTo(item) && instance != item)
                return true;
        }

        return false;
    }


    public static (MethodInfo method, Type dtoType) GetMethod(string requestName, Type theService)
    {
        var methods = GetActions(requestName, theService);
        MethodInfo method = null;
        Type paramType = null;
        foreach (var mi in methods)
        {
            var parameters = mi.GetParameters();
            if (parameters.Length <= 0 || parameters.Length > 2) continue;

            var parameterString = parameters.FirstOrDefault().ParameterType.ToString();
            var individualParameter = parameterString?.Split('.').LastOrDefault();
            if (individualParameter == requestName ||
                string.IsNullOrWhiteSpace(requestName))
            {
                // Prefer a recognized action-verb handler (Any/AnyAsync/Get/Post). This makes
                // selection deterministic and prevents an unintended same-signature helper method
                // from shadowing the real endpoint when several methods share the parameter type.
                if (method == null || (!IsActionVerb(method.Name) && IsActionVerb(mi.Name)))
                {
                    method = mi;
                    paramType = parameters.FirstOrDefault().ParameterType;
                }
            }
        }

        return (method, paramType);
    }

    public async Task<T?> GetAttributeAsync<T>(Type service)
    {
        var attribute = Attribute.GetCustomAttributes(
            service)?.Where(p => p.GetType().GetInterfaces().Contains(typeof(T)))?.FirstOrDefault();
        var secureAttType = attribute?.GetType();
        if (secureAttType == null) return default;

        var ctors = secureAttType.GetConstructors();
        // assuming class A has only one constructor
        var ctor = ctors
            .Where(ConstructorAvailable)
            .OrderByDescending(p => p.GetParameters().Count())
            .FirstOrDefault();

        var ctorParameters = await InitalizeParameters(ctor);

        var instance = ctor.Invoke(ctorParameters.ToArray());
        SetNewObjectsProperties(attribute!, instance, secureAttType);
        return (T)instance;
    }

    private static bool ConstructorAvailable(ConstructorInfo constructor)
    {
        var hasIgnoreAttribute = constructor?.GetCustomAttributes()
            ?.Any(p => p.GetType().GetInterfaces().Contains(typeof(IIgnoreConstructorAttribute)));
        return !hasIgnoreAttribute.HasValue || !hasIgnoreAttribute.Value;
    }

    private static void SetNewObjectsProperties(object existingObject, object newObject, Type type)
    {
        var properties = type.GetProperties()
                    .Where(props => props.CanRead && props.CanWrite 
                    && props.GetGetMethod(false) != null && props.GetSetMethod(false) != null);
        foreach (var prop in properties)
        {
            prop.SetValue(newObject, prop.GetValue(existingObject));
        }
    }

    public async Task<IExecutableAttribute> GetDescriptionAttributeAsync(Type service, object dto)
    {
        var secureAttType = Attribute.GetCustomAttributes(
                service)?.Where(p => p.GetType().GetInterfaces().Contains(typeof(DescriptionAttribute)))
            ?.FirstOrDefault()
            ?.GetType();
        if (secureAttType == null) return null;

        var ctors = secureAttType.GetConstructors();
        // assuming class A has only one constructor
        var ctor = ctors
            .Where(ConstructorAvailable)
            .OrderByDescending(p => p.GetParameters().Count())
            .FirstOrDefault();

        var ctorParameters = await InitalizeParameters(ctor);

        var instance = ctor.Invoke(ctorParameters.ToArray());
        return (IExecutableAttribute)instance;
    }

    public static IEnumerable<MethodInfo> GetActions(string requestName, Type serviceType)
    {
        foreach (var mi in serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
        {
            // Never treat methods inherited from System.Object (Equals, etc.) as dispatchable
            // endpoints. object.Equals(object) has a reference-type parameter and would otherwise
            // be remotely invocable via /.../Object.
            if (mi.DeclaringType == typeof(object))
                continue;

            var parameters = mi.GetParameters();

            if (mi.IsGenericMethod || parameters.Length <= 0 || parameters.Length > 2)
                continue;

            var paramType = parameters[0].ParameterType;
            if (paramType.IsValueType || paramType == typeof(string))
                continue;

            if (!IsActionVerb(mi.Name))
            {
                if (string.Equals(paramType.Name, requestName, StringComparison.InvariantCultureIgnoreCase))
                    yield return mi;

                continue;
            }

            yield return mi;
        }
    }

    private static bool IsActionVerb(string methodName)
    {
        var name = methodName.ToUpperInvariant();
        return name == "ANY" || name == "ANYASYNC" || name == "POST" || name == "GET";
    }
}
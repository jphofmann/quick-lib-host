using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.Logging.Support.Logging;

namespace QuickHost
{

    public class ServiceStackHost : AppHostHttpListenerBase
    {
        private readonly Dictionary<string, Type> _restRouteMap;

        private 
            ServiceStackHost(
                string name, List<Assembly> assembliesWithServices, Dictionary<string, Type> restRouteMap) 
            : base("QuickHost: " + name, assembliesWithServices.ToArray())
        {
            _restRouteMap = restRouteMap;
        }

        public static ServiceStackHost CreateFrom(string serviceName, object quickHostableClass)
        {
            Dictionary<string, Type> restRouteMap;
            List<Assembly> assembliesWithServices;

            GenerateServiceInterfaceAssemblies(
                serviceName, quickHostableClass, out assembliesWithServices, out restRouteMap);

            return new ServiceStackHost(serviceName, assembliesWithServices, restRouteMap);   
        }

        public override void Configure(Funq.Container container)
        {
            SetConfig( 
                new EndpointHostConfig {
                    WsdlSoapActionNamespace = "http://api.quickhost.org/data",
                    WsdlServiceNamespace = "http://api.quickhost.org/data",
                    LogFactory = new DebugLogFactory()});

            foreach (var route in _restRouteMap.Keys)
            {
                Routes.Add(_restRouteMap[route], "/" + route, "GET");
            }
        }

        // Informed by http://stackoverflow.com/questions/3862226/dynamically-create-a-class-in-c-sharp
        private static void 
            GenerateServiceInterfaceAssemblies(
                string serviceName, 
                object quickHostableClass, 
                out List<Assembly> assembliesWithServices, 
                out Dictionary<string, Type> restRouteMap)
        {
            assembliesWithServices = new List<Assembly>();
            restRouteMap = new Dictionary<string, Type>();

            QuickHostableClassMappings.AddHostedClass(serviceName, quickHostableClass);

            var methodsToHost = new Dictionary<MethodInfo, QuickHostMethodAttribute>();

            foreach (var methodInfo in quickHostableClass.GetType().GetMethods())
            {
                foreach (var attr in methodInfo.GetCustomAttributes(typeof(QuickHostMethodAttribute), true))
                {
                    methodsToHost.Add(methodInfo, (QuickHostMethodAttribute)attr);
                    break;
                }
            }

            if (methodsToHost.Count == 0)
            {
                throw new Exception(String.Format("{0} has no methods to host.", serviceName));
            }

            /*
            var typesToHost = 
                quickHostableClass.GetType().GetNestedTypes()
                    .Where(type => type.GetCustomAttributes(typeof (QuickHostTypeAttribute), true).Length > 0)
                    .ToList();
            */

            #region Define some attributes.
            
            var assemblyVersionAttribute =
                new CustomAttributeBuilder(
                    typeof (AssemblyVersionAttribute).GetConstructor(
                        new[] {typeof (string)}), new object[] {"1.0.0"});

            var dataContractAttribute =
                new CustomAttributeBuilder(
                        typeof(DataContractAttribute).GetConstructor(new Type[] { }),
                        new object[] { },
                        new[] { typeof(DataContractAttribute).GetProperty("Namespace") },
                        new object[] { "http://api.quickhost.org/data" });

            const TypeAttributes someTypeAttributes =
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout;

            const MethodAttributes someMethodAttributes =
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            
            #endregion

            // Construct assembly.

            var assemblyName = new AssemblyName(String.Format("{0}DynamicServiceInterface", serviceName));
            
            var assemblyBuilder = 
                AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            
            assemblyBuilder
                .DefineVersionInfoResource(
                    String.Format("{0} by QuickHost Dynamic Service Generator", serviceName), 
                    "1.0.0.0", 
                    "Quick Host Group", 
                    "(c) 2013 Quick Host Group", 
                    null);

            assemblyBuilder.SetCustomAttribute(assemblyVersionAttribute);

            // Add hosted methods.

            var assemblyModuleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            foreach (var hostedMethodInfo in methodsToHost.Keys)
            {
                var methodAlias = methodsToHost[hostedMethodInfo].MethodAlias;

                // Build Request DTO

                var requestTypeBuilder = 
                    assemblyModuleBuilder.DefineType(
                        assemblyName.Name + "." + methodAlias, someTypeAttributes, null);
                
                requestTypeBuilder.SetCustomAttribute(dataContractAttribute);
                requestTypeBuilder.DefineDefaultConstructor(someMethodAttributes);

                foreach (var parameterInfo in hostedMethodInfo.GetParameters())
                {
                    CreatePropertyFromParameter(requestTypeBuilder, parameterInfo); 
                }

                var request = requestTypeBuilder.CreateType();

                if (request == null)
                {
                    throw new Exception(String.Format("Unable to create type '{0}'.", requestTypeBuilder.Name));
                }

                // Build Response DTO
                var responseTypeBuilder = 
                    assemblyModuleBuilder.DefineType(
                        assemblyName.Name + "." + methodAlias + "Response", 
                        someTypeAttributes,
                        hostedMethodInfo.ReturnParameter == null 
                            ? null : hostedMethodInfo.ReturnParameter.ParameterType);
                
                responseTypeBuilder.SetCustomAttribute(dataContractAttribute);
                responseTypeBuilder.DefineDefaultConstructor(someMethodAttributes);
                responseTypeBuilder.CreateType();

                // Build service class.

                var serviceTypeBuilder = 
                    assemblyModuleBuilder.DefineType(
                        assemblyName.Name + "." + methodAlias + "Service", 
                        someTypeAttributes, 
                        typeof(ServiceStack.ServiceInterface.Service));

                serviceTypeBuilder.DefineDefaultConstructor(someMethodAttributes);

                var anyMethodILGenerator = 
                    serviceTypeBuilder.DefineMethod(
                        "Any", MethodAttributes.Public, typeof(object), new [] { request }).GetILGenerator();

                var anyMethodParameterLocalBuilders = new List<LocalBuilder>();

                foreach (var parameterInfo in hostedMethodInfo.GetParameters())
                {
                    var parameterLocalBuilder = anyMethodILGenerator.DeclareLocal(parameterInfo.ParameterType);
                    anyMethodILGenerator.Emit(OpCodes.Ldarg_1);
                    
                    anyMethodILGenerator
                        .Emit(
                            OpCodes.Call, 
                            request.GetProperties().First(x => x.Name == parameterInfo.Name).GetGetMethod());

                    anyMethodILGenerator.Emit(OpCodes.Stloc, parameterLocalBuilder.LocalIndex);
                    anyMethodParameterLocalBuilders.Add(parameterLocalBuilder);
                }

                anyMethodILGenerator.Emit(OpCodes.Ldstr, serviceName);
                
                anyMethodILGenerator
                    .Emit(OpCodes.Call, typeof(QuickHostableClassMappings).GetMethod("GetHostedClass"));
                
                foreach (var localBuilder in anyMethodParameterLocalBuilders)
                    anyMethodILGenerator.Emit(OpCodes.Ldloc, localBuilder.LocalIndex);

                anyMethodILGenerator.EmitWriteLine("Calling " + hostedMethodInfo.Name + ".");
                anyMethodILGenerator.Emit(OpCodes.Call, hostedMethodInfo);
                anyMethodILGenerator.Emit(OpCodes.Ret);

                assembliesWithServices.Add(serviceTypeBuilder.CreateType().Assembly);

                if (methodsToHost[hostedMethodInfo].RestUriAlias == null)
                {
                    // No rest routes defined.
                    continue;
                }
                
                // Generate rest route information.

                // produce /RestUriAlias/{Arg1}/{Arg2} style URI.
                var uri = 
                    String.Format(
                        "{0}{1}",
                        methodsToHost[hostedMethodInfo].RestUriAlias,
                        hostedMethodInfo.GetParameters().Aggregate("", (sd, pi) => sd + ("/{" + pi.Name + "}")));

                restRouteMap[uri] = request;
            }
        }

        private static void CreatePropertyFromParameter(TypeBuilder typeBuilder, ParameterInfo parameterInfo)
        {
            var propertyBuilder =
                typeBuilder.DefineProperty(
                    parameterInfo.Name, PropertyAttributes.HasDefault, parameterInfo.ParameterType, null);

            var fieldBuilder =
                typeBuilder.DefineField(
                    "_" + parameterInfo.Name, parameterInfo.ParameterType, FieldAttributes.Private);

            // Create get method.

            var getMethodBuilder = 
                typeBuilder.DefineMethod(
                    "get_" + parameterInfo.Name,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    parameterInfo.ParameterType, 
                    Type.EmptyTypes);

            var getMethodILGenerator = getMethodBuilder.GetILGenerator();

            getMethodILGenerator.Emit(OpCodes.Ldarg_0);
            getMethodILGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            getMethodILGenerator.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getMethodBuilder);

            // Create set method

            var setMethodBuilder =
                typeBuilder.DefineMethod(
                    "set_" + parameterInfo.Name,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null,
                    new[] { parameterInfo.ParameterType });

            var setMethodILGenerator = setMethodBuilder.GetILGenerator();
            
            setMethodILGenerator.MarkLabel(setMethodILGenerator.DefineLabel());
            setMethodILGenerator.Emit(OpCodes.Ldarg_0);
            setMethodILGenerator.Emit(OpCodes.Ldarg_1);
            setMethodILGenerator.Emit(OpCodes.Stfld, fieldBuilder);

            setMethodILGenerator.Emit(OpCodes.Nop);
            setMethodILGenerator.MarkLabel(setMethodILGenerator.DefineLabel());
            setMethodILGenerator.Emit(OpCodes.Ret);

            propertyBuilder.SetSetMethod(setMethodBuilder);

            propertyBuilder
                .SetCustomAttribute(
                    new CustomAttributeBuilder(
                        typeof(DataMemberAttribute).GetConstructor(new Type[] { }), new object[] { }));              
        }
    }    
}

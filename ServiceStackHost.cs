using ServiceStack.Logging.Support.Logging;
using ServiceStack.WebHost.Endpoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

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


        //krk begin - Can these be made private?
        //public class ResponseStatus
        //{
        //    public int rc { get; set; }
        //    public string errorMessage { get; set; }
        //}
        //krk end

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

                //KRK Build Combined Response Type (ResponseStatus + OriginalResponseType)
                //var combinedResponseTypeBuilder =
                //    assemblyModuleBuilder.DefineType(
                //        assemblyName.Name + "." + methodAlias + "CombinedResponseType",
                //        someTypeAttributes, null);

                // Build Response DTO
                var responseTypeBuilder = 
                    assemblyModuleBuilder.DefineType(
                        assemblyName.Name + "." + methodAlias + "Response", 
                        someTypeAttributes, null );
                if( hostedMethodInfo.ReturnParameter != null )
                {
                    CreatePropertyFromParameter(responseTypeBuilder, hostedMethodInfo.ReturnParameter);
                    CreateResponseProperty(responseTypeBuilder);
                    //CreateResponsePropertyFromParameter(combinedResponseTypeBuilder, responseTypeBuilder, hostedMethodInfo.ReturnParameter);
                    //CreateResponsePropertyFromParameter(responseTypeBuilder, hostedMethodInfo.ReturnParameter);
                }

                responseTypeBuilder.SetCustomAttribute(dataContractAttribute);
                responseTypeBuilder.DefineDefaultConstructor(someMethodAttributes);
                Type responseType = responseTypeBuilder.CreateType();

                // Build service class.

                var serviceTypeBuilder = 
                    assemblyModuleBuilder.DefineType(
                        assemblyName.Name + "." + methodAlias + "Service", 
                        someTypeAttributes, 
                        typeof(ServiceStack.ServiceInterface.Service));

                serviceTypeBuilder.DefineDefaultConstructor(someMethodAttributes);

                var anyMethodILGenerator = 
                    serviceTypeBuilder.DefineMethod(
                        "Any", MethodAttributes.Public, responseType, new [] { request }).GetILGenerator();

                //krk begin Try Catch logic
                //Label lblTry = anyMethodILGenerator.BeginExceptionBlock();

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
                if( hostedMethodInfo.ReturnParameter != null )
                {
                    //Discover Golden Begin
                    var rtc = responseType.GetConstructor(new Type[] { });
                    var setMethod = responseType.GetProperties().First(x => x.Name == "result").GetSetMethod();
                    var resultParam = anyMethodILGenerator.DeclareLocal(hostedMethodInfo.ReturnParameter.ParameterType);
                    var wrappedParam = anyMethodILGenerator.DeclareLocal(responseType);
                    anyMethodILGenerator.Emit(OpCodes.Stloc, resultParam.LocalIndex);
                    anyMethodILGenerator.Emit(OpCodes.Newobj, rtc);
                    anyMethodILGenerator.Emit(OpCodes.Stloc, wrappedParam.LocalIndex);
                    anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                    anyMethodILGenerator.Emit(OpCodes.Ldloc, resultParam.LocalIndex);
                    anyMethodILGenerator.Emit(OpCodes.Call, setMethod);
                    anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                    //Discover Golden End

                    //var rtc = responseType.GetConstructor(new Type[] { });
                    //var setMethod = responseType.GetProperties().First(x => x.Name == "result").GetSetMethod();
                    //var setResponseMethod = responseType.GetProperties().First(x => x.Name.StartsWith("response")).GetSetMethod();
                    //var resultParam = anyMethodILGenerator.DeclareLocal(hostedMethodInfo.ReturnParameter.ParameterType);
                    //var responseStatusParam = anyMethodILGenerator.DeclareLocal(typeof(Int32));
                    //var wrappedParam = anyMethodILGenerator.DeclareLocal(responseType);
                    //anyMethodILGenerator.Emit(OpCodes.Stloc, resultParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Newobj, rtc);
                    //anyMethodILGenerator.Emit(OpCodes.Stloc, wrappedParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, resultParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Call, setMethod);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);

                    //anyMethodILGenerator.Emit(OpCodes.Stloc, responseStatusParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Newobj, rtc);
                    //anyMethodILGenerator.Emit(OpCodes.Stloc, wrappedParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, responseStatusParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Call, setResponseMethod);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);


                    //krk
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc_S);

                    //var rcConstructor = responseType.GetConstructor(new Type[] { typeof(Int32) });
                    //var setResponseMethod = responseType.GetProperties().First(x => x.Name.StartsWith("response")).GetSetMethod();
                    //var resultResponseParam = anyMethodILGenerator.DeclareLocal(hostedMethodInfo.ReturnParameter.ParameterType);
                    //var wrappedParam = anyMethodILGenerator.DeclareLocal(responseType);
                    //anyMethodILGenerator.Emit(OpCodes.Stloc, resultParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Newobj, rcConstructor);
                    //anyMethodILGenerator.Emit(OpCodes.Stloc, wrappedParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, resultParam.LocalIndex);
                    //anyMethodILGenerator.Emit(OpCodes.Call, setResponseMethod);
                    //anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                    //krk
                    //var setResponseMethod = responseType.GetProperties().First(x => x.Name.StartsWith("response")).GetSetMethod();

                    //anyMethodILGenerator.Emit(OpCodes.Call, setResponseMethod);
                    //krk end

                    anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                }

                //krk begin
                //LocalBuilder responseStatus = anyMethodILGenerator.DeclareLocal(typeof(int));
                //LocalBuilder thrownException = anyMethodILGenerator.DeclareLocal(typeof(Exception));

                //anyMethodILGenerator.Emit(OpCodes.Ldc_I4_8);
                //anyMethodILGenerator.Emit(OpCodes.Stloc, responseStatus);

                //anyMethodILGenerator.Emit(OpCodes.Leave, lblTry);
                //anyMethodILGenerator.BeginCatchBlock(typeof(Exception));
                
                //// On entry to the catch block, the thrown exception is on the stack. Store it in a local variable.                
                ////anyMethodILGenerator.Emit(OpCodes.Stloc_S, thrownException);
                ////anyMethodILGenerator.Emit(OpCodes.Stloc, responseStatus);

                //// This is the end of the try/catch/finally block.
                //anyMethodILGenerator.Emit(OpCodes.Leave_S, lblTry);
                //anyMethodILGenerator.EndExceptionBlock();

                //anyMethodILGenerator.Emit(OpCodes.Ldloc, responseStatus);
                //krk end


                anyMethodILGenerator.Emit(OpCodes.Ret);
                serviceTypeBuilder.CreateType();
                
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

            assembliesWithServices.Add(assemblyModuleBuilder.Assembly);
        }

        //Discover Golden Begin
        private static void CreatePropertyFromParameter(TypeBuilder typeBuilder, ParameterInfo parameterInfo)
        {
            var parameterName = parameterInfo.Name != null ? parameterInfo.Name : "result";
            var propertyBuilder =
                typeBuilder.DefineProperty(
                    parameterName, PropertyAttributes.HasDefault, parameterInfo.ParameterType, null);

            var fieldBuilder =
                typeBuilder.DefineField(
                    "_" + parameterName, parameterInfo.ParameterType, FieldAttributes.Private);

            // Create get method.

            var getMethodBuilder =
                typeBuilder.DefineMethod(
                    "get_" + parameterName,
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
                    "set_" + parameterName,
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
        //Discover Golden End

        //krk - Create Response Property
        private static void CreateResponseProperty(TypeBuilder typeBuilder)
        {
            string responseName = "responseStatus";
            //Type responseType = typeof(ResponseStatus);
            Type responseType = typeof(Int32);

            var propertyBuilder = typeBuilder.DefineProperty(responseName, PropertyAttributes.HasDefault, responseType, null);
            propertyBuilder.SetConstant(99);

            var fieldBuilder =
                typeBuilder.DefineField(
                    "_" + responseName, responseType, FieldAttributes.Private);

            // Create get method.

            var getMethodBuilder =
                typeBuilder.DefineMethod(
                    "get_" + responseName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    responseType,
                    Type.EmptyTypes);

            var getMethodILGenerator = getMethodBuilder.GetILGenerator();

            getMethodILGenerator.Emit(OpCodes.Ldarg_0);
            getMethodILGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            getMethodILGenerator.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getMethodBuilder);

            // Create set method

            var setMethodBuilder =
                typeBuilder.DefineMethod(
                    "set_" + responseName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null,
                    new[] { responseType });

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

        //private static void CreateResponsePropertyFromParameter(TypeBuilder typeBuilder, ParameterInfo parameterInfo)
        //{
        //    var combinedResponseTypeBuilder =
        //        assemblyModuleBuilder.DefineType(
        //            assemblyName.Name + "." + methodAlias + "CombinedResponseType",
        //            someTypeAttributes, null);

        //    Type responseType = CreateResponseType(combinedTypeBuilder, parameterInfo);

        //    var parameterName = parameterInfo.Name != null ? parameterInfo.Name : "result";
        //    var propertyBuilder =
        //        typeBuilder.DefineProperty(
        //            parameterName, PropertyAttributes.HasDefault, responseType, null);

        //    var fieldBuilder =
        //        typeBuilder.DefineField(
        //            "_" + parameterName, responseType, FieldAttributes.Private);

        //    // Create get method.

        //    var getMethodBuilder =
        //        typeBuilder.DefineMethod(
        //            "get_" + parameterName,
        //            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
        //            responseType,
        //            Type.EmptyTypes);

        //    var getMethodILGenerator = getMethodBuilder.GetILGenerator();

        //    getMethodILGenerator.Emit(OpCodes.Ldarg_0);
        //    getMethodILGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
        //    getMethodILGenerator.Emit(OpCodes.Ret);

        //    propertyBuilder.SetGetMethod(getMethodBuilder);

        //    // Create set method

        //    var setMethodBuilder =
        //        typeBuilder.DefineMethod(
        //            "set_" + parameterName,
        //            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
        //            null,
        //            new[] { responseType });

        //    var setMethodILGenerator = setMethodBuilder.GetILGenerator();

        //    setMethodILGenerator.MarkLabel(setMethodILGenerator.DefineLabel());
        //    setMethodILGenerator.Emit(OpCodes.Ldarg_0);
        //    setMethodILGenerator.Emit(OpCodes.Ldarg_1);
        //    setMethodILGenerator.Emit(OpCodes.Stfld, fieldBuilder);

        //    setMethodILGenerator.Emit(OpCodes.Nop);
        //    setMethodILGenerator.MarkLabel(setMethodILGenerator.DefineLabel());
        //    setMethodILGenerator.Emit(OpCodes.Ret);

        //    propertyBuilder.SetSetMethod(setMethodBuilder);

        //    propertyBuilder
        //        .SetCustomAttribute(
        //            new CustomAttributeBuilder(
        //                typeof(DataMemberAttribute).GetConstructor(new Type[] { }), new object[] { }));
        //}

        //KRK Begin
        //private static void CreatePropertyFromParameter(TypeBuilder typeBuilder, ParameterInfo parameterInfo, bool blnAddResponse)
        //{
        //    var parameterName = parameterInfo.Name != null ? parameterInfo.Name : "result";
        //    PropertyBuilder propertyBuilder =
        //        typeBuilder.DefineProperty(
        //            parameterName, PropertyAttributes.HasDefault, parameterInfo.ParameterType, null);

        //    FieldBuilder fieldBuilder =
        //        typeBuilder.DefineField(
        //            "_" + parameterName, parameterInfo.ParameterType, FieldAttributes.Private);

        //    var getMethodBuilder =
        //        typeBuilder.DefineMethod(
        //            "get_" + parameterName,
        //            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
        //            parameterInfo.ParameterType,
        //            Type.EmptyTypes);

        //    var getMethodILGenerator = getMethodBuilder.GetILGenerator();

        //    getMethodILGenerator.Emit(OpCodes.Ldarg_0);
        //    getMethodILGenerator.Emit(OpCodes.Ldfld, fieldBuilder);

        //    //krk begin
        //    //if (blnAddResponse)
        //    //{
        //    //    PropertyBuilder responsePropertyBuilder = typeBuilder.DefineProperty("responseStatus", PropertyAttributes.HasDefault, typeof(ResponseStatus), null);
        //    //    FieldBuilder responseFieldBuilder = typeBuilder.DefineField("_responseStatus", typeof(ResponseStatus), FieldAttributes.Private);
        //    // //   getMethodILGenerator.Emit(OpCodes.Ldfld, responseFieldBuilder);
        //    //}
        //    //krk end            

        //    getMethodILGenerator.Emit(OpCodes.Ret);

        //    propertyBuilder.SetGetMethod(getMethodBuilder);

        //    // Create set method

        //    var setMethodBuilder =
        //        typeBuilder.DefineMethod(
        //            "set_" + parameterName,
        //            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
        //            null,
        //            new[] { parameterInfo.ParameterType });

        //    var setMethodILGenerator = setMethodBuilder.GetILGenerator();

        //    setMethodILGenerator.MarkLabel(setMethodILGenerator.DefineLabel());
        //    setMethodILGenerator.Emit(OpCodes.Ldarg_0);
        //    setMethodILGenerator.Emit(OpCodes.Ldarg_1);
        //    setMethodILGenerator.Emit(OpCodes.Stfld, fieldBuilder);

        //    setMethodILGenerator.Emit(OpCodes.Nop);
        //    setMethodILGenerator.MarkLabel(setMethodILGenerator.DefineLabel());
        //    setMethodILGenerator.Emit(OpCodes.Ret);

        //    propertyBuilder.SetSetMethod(setMethodBuilder);

        //    propertyBuilder
        //        .SetCustomAttribute(
        //            new CustomAttributeBuilder(
        //                typeof(DataMemberAttribute).GetConstructor(new Type[] { }), new object[] { }));
        //}

        //private static void CreateResponsePropertyFromParameter(TypeBuilder combinedTypeBuilder, TypeBuilder typeBuilder, ParameterInfo parameterInfo)
        //{
        //    Type responseType = CreateResponseType(combinedTypeBuilder, parameterInfo);

        //    var parameterName = parameterInfo.Name != null ? parameterInfo.Name : "result";
        //    var propertyBuilder =
        //        typeBuilder.DefineProperty(
        //            parameterName, PropertyAttributes.HasDefault, responseType, null);

        //    var fieldBuilder =
        //        typeBuilder.DefineField(
        //            "_" + parameterName, responseType, FieldAttributes.Private);

        //    // Create get method.

        //    var getMethodBuilder =
        //        typeBuilder.DefineMethod(
        //            "get_" + parameterName,
        //            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
        //            responseType,
        //            Type.EmptyTypes);

        //    var getMethodILGenerator = getMethodBuilder.GetILGenerator();

        //    getMethodILGenerator.Emit(OpCodes.Ldarg_0);
        //    getMethodILGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
        //    getMethodILGenerator.Emit(OpCodes.Ret);

        //    propertyBuilder.SetGetMethod(getMethodBuilder);

        //    // Create set method

        //    var setMethodBuilder =
        //        typeBuilder.DefineMethod(
        //            "set_" + parameterName,
        //            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
        //            null,
        //            new[] { responseType });

        //    var setMethodILGenerator = setMethodBuilder.GetILGenerator();

        //    setMethodILGenerator.MarkLabel(setMethodILGenerator.DefineLabel());
        //    setMethodILGenerator.Emit(OpCodes.Ldarg_0);
        //    setMethodILGenerator.Emit(OpCodes.Ldarg_1);
        //    setMethodILGenerator.Emit(OpCodes.Stfld, fieldBuilder);

        //    setMethodILGenerator.Emit(OpCodes.Nop);
        //    setMethodILGenerator.MarkLabel(setMethodILGenerator.DefineLabel());
        //    setMethodILGenerator.Emit(OpCodes.Ret);

        //    propertyBuilder.SetSetMethod(setMethodBuilder);

        //    propertyBuilder
        //        .SetCustomAttribute(
        //            new CustomAttributeBuilder(
        //                typeof(DataMemberAttribute).GetConstructor(new Type[] { }), new object[] { }));
        //}

        //This successfully adds two properties - but the ResponseStatus is not inside of the result
        //private static Type CreateResponseType(TypeBuilder combinedTypeBuilder, ParameterInfo parameterInfo)
        //{
        //     ConstructorBuilder constructor = combinedTypeBuilder.DefineDefaultConstructor(
        //         MethodAttributes.Public |
        //         MethodAttributes.SpecialName |
        //         MethodAttributes.RTSpecialName);

        //    //Add response status to the object
        //    CreateProperty(combinedTypeBuilder, "responseStatus", typeof(ResponseStatus) );

        //    //Add original properties to the object
        //    var parameterName = parameterInfo.Name != null ? parameterInfo.Name : "result";
        //    CreateProperty(combinedTypeBuilder, parameterName, parameterInfo.ParameterType);

        //    // make the type
        //    Type t = combinedTypeBuilder.CreateType();

        //    return t;
        //}

        //private static Type CreateResponseType(TypeBuilder combinedTypeBuilder, ParameterInfo parameterInfo)
        //{
        //    ConstructorBuilder constructor = combinedTypeBuilder.DefineDefaultConstructor(
        //        MethodAttributes.Public |
        //        MethodAttributes.SpecialName |
        //        MethodAttributes.RTSpecialName);

        //    //Add response status to the object
        //    var parameterName = parameterInfo.Name != null ? parameterInfo.Name : "result";
        //    CreatePropertyWithResponse(combinedTypeBuilder, parameterName, parameterInfo.ParameterType);

        //    //Add original properties to the object
        //    //var parameterName = parameterInfo.Name != null ? parameterInfo.Name : "result";
        //    //CreateProperty(combinedTypeBuilder, parameterName, parameterInfo.ParameterType);

        //    // make the type
        //    Type t = combinedTypeBuilder.CreateType();

        //    return t;
        //}

        private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
        {
            FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

            PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMthdBldr =
                tb.DefineMethod("set_" + propertyName,
                  MethodAttributes.Public |
                  MethodAttributes.SpecialName |
                  MethodAttributes.HideBySig,
                  null, new[] { propertyType });

            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
            Label modifyProperty = setIl.DefineLabel();
            Label exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);

            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getPropMthdBldr);
            propertyBuilder.SetSetMethod(setPropMthdBldr);
        }

        //private static void CreatePropertyWithResponse(TypeBuilder tb, string propertyName, Type propertyType)
        //{
        //    FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
        //    FieldBuilder responseFieldBuilder = tb.DefineField("_ResponseType", typeof(ResponseStatus), FieldAttributes.Private);

        //    PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
        //    MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
        //    ILGenerator getIl = getPropMthdBldr.GetILGenerator();

        //    getIl.Emit(OpCodes.Ldarg_0);
        //    getIl.Emit(OpCodes.Ldfld, responseFieldBuilder);
        //    getIl.Emit(OpCodes.Ldfld, fieldBuilder);
        //    getIl.Emit(OpCodes.Ret);

        //    MethodBuilder setPropMthdBldr =
        //        tb.DefineMethod("set_" + propertyName,
        //          MethodAttributes.Public |
        //          MethodAttributes.SpecialName |
        //          MethodAttributes.HideBySig,
        //          null, new[] { propertyType });

        //    ILGenerator setIl = setPropMthdBldr.GetILGenerator();
        //    Label modifyProperty = setIl.DefineLabel();
        //    Label exitSet = setIl.DefineLabel();

        //    setIl.MarkLabel(modifyProperty);
        //    setIl.Emit(OpCodes.Ldarg_0);
        //    setIl.Emit(OpCodes.Ldarg_1);
        //    setIl.Emit(OpCodes.Stfld, fieldBuilder);

        //    setIl.Emit(OpCodes.Nop);
        //    setIl.MarkLabel(exitSet);
        //    setIl.Emit(OpCodes.Ret);

        //    propertyBuilder.SetGetMethod(getPropMthdBldr);
        //    propertyBuilder.SetSetMethod(setPropMthdBldr);
        //}
        //KRK End
    }    
}

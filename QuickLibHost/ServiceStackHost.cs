/* 
 * Copyright (C) 2013 the QuickLibHost contributors. All rights reserved.
 * 
 * This file is part of QuickLibHost.
 * 
 * QuickLibHost is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * QuickLibHost is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.

 * You should have received a copy of the GNU Lesser General Public License
 * along with QuickLibHost.  If not, see <http://www.gnu.org/licenses/>.
 */

using QuickLibHostClient;
using ServiceStack.Logging.Support.Logging;
using ServiceStack.WebHost.Endpoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace QuickLibHost
{
    public class ServiceStackHost : AppHostHttpListenerBase
    {
        //Constants for the standard properties
        private const string PROP_NAME_RC = "ReturnCode";
        private const string PROP_NAME_ERROR = "ErrorMessage";
        private const string PROP_NAME_RESULT = "Result";

        public Dictionary<string, Type> _restRouteMap;

        private 
            ServiceStackHost(
                string name, List<Assembly> assembliesWithServices, Dictionary<string, Type> restRouteMap) 
            : base("QuickLibHost: " + name, assembliesWithServices.ToArray())
        {
            _restRouteMap = restRouteMap;
        }

        public static ServiceStackHost CreateFrom(string serviceName, object QuickLibHostableClass)
        {
            Dictionary<string, Type> restRouteMap;
            List<Assembly> assembliesWithServices;

            GenerateServiceInterfaceAssemblies(
                serviceName, QuickLibHostableClass, out assembliesWithServices, out restRouteMap);

            return new ServiceStackHost(serviceName, assembliesWithServices, restRouteMap);   
        }

        public override void Configure(Funq.Container container)
        {
            SetConfig( 
                new EndpointHostConfig {
                    WsdlSoapActionNamespace = "http://api.quicklibhost.org/data",
                    WsdlServiceNamespace = "http://api.quicklibhost.org/data",
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
                object quickLibHostableClass, 
                out List<Assembly> assembliesWithServices, 
                out Dictionary<string, Type> restRouteMap)
        {
            assembliesWithServices = new List<Assembly>();
            restRouteMap = new Dictionary<string, Type>();

            QuickLibHostableClassMappings.AddHostedClass(serviceName, quickLibHostableClass);

            var methodsToHost = new Dictionary<MethodInfo, QuickLibHostMethodAttribute>();

            foreach (var methodInfo in quickLibHostableClass.GetType().GetMethods())
            {
                foreach (var attr in methodInfo.GetCustomAttributes(typeof(QuickLibHostMethodAttribute), true))
                {
                    methodsToHost.Add(methodInfo, (QuickLibHostMethodAttribute)attr);
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
                        new object[] { "http://api.quicklibhost.org/data" });
            
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
                    String.Format("{0} by QuickLibHost Dynamic Service Generator", serviceName), 
                    "1.0.0.0", 
                    "QuickLibHost Group", 
                    "(c) 2013 QuickLibHost Group", 
                    null);

            assemblyBuilder.SetCustomAttribute(assemblyVersionAttribute);

            // Add hosted methods.

            var assemblyModuleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            foreach (var hostedMethodInfo in methodsToHost.Keys)
            {
                var methodAlias = methodsToHost[hostedMethodInfo].MethodAlias;

                // Build Request DTO
                TypeBuilder requestTypeBuilder =
                    assemblyModuleBuilder.DefineType(
                        assemblyName.Name + "." + methodAlias, someTypeAttributes, null);

                requestTypeBuilder.SetCustomAttribute(dataContractAttribute);
                requestTypeBuilder.DefineDefaultConstructor(someMethodAttributes);

                foreach (var parameterInfo in hostedMethodInfo.GetParameters())
                {
                    CreatePropertyFromParameter(requestTypeBuilder, parameterInfo);
                }
                var request = requestTypeBuilder.CreateType();

                TypeBuilder responseTypeBuilder =
                    assemblyModuleBuilder.DefineType(
                        assemblyName.Name + "." + methodAlias + "Response", someTypeAttributes, null);

                if (hostedMethodInfo.ReturnParameter != null)
                {
                    CreatePropertyFromParameter(responseTypeBuilder, hostedMethodInfo.ReturnParameter);
                }

                //Add standard response properties
                CreateProperty(responseTypeBuilder, PROP_NAME_RC, typeof(int));
                CreateProperty(responseTypeBuilder, PROP_NAME_ERROR, typeof(string));

                responseTypeBuilder.SetCustomAttribute(dataContractAttribute);
                responseTypeBuilder.DefineDefaultConstructor(someMethodAttributes);

                var responseType = responseTypeBuilder.CreateType();
                if (responseType == null)
                {
                    throw new Exception(String.Format("Unable to create type '{0}'.", responseType.Name));
                }

                // Build service class.
                var serviceTypeBuilder =
                    assemblyModuleBuilder.DefineType(
                        assemblyName.Name + "." + methodAlias + "Service",
                        someTypeAttributes,
                        typeof(ServiceStack.ServiceInterface.Service));

                serviceTypeBuilder.DefineDefaultConstructor(someMethodAttributes);

                ILGenerator anyMethodILGenerator =
                    serviceTypeBuilder.DefineMethod(
                        "Any", MethodAttributes.Public, responseType, new[] { request }).GetILGenerator();

                var anyMethodParameterLocalBuilders = new List<LocalBuilder>();
                
                //Begin Try/Catch/Finally exception block
                Label lblExCatch = anyMethodILGenerator.BeginExceptionBlock();

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
                    .Emit(OpCodes.Call, typeof(QuickLibHostableClassMappings).GetMethod("GetHostedClass"));

                foreach (var localBuilder in anyMethodParameterLocalBuilders)
                    anyMethodILGenerator.Emit(OpCodes.Ldloc, localBuilder.LocalIndex);

                anyMethodILGenerator.EmitWriteLine("Calling " + hostedMethodInfo.Name + ".");

                //Discover Golden Begin
                //var rtc = responseType.GetConstructor(new Type[] { });
                //var setMethod = responseType.GetProperties().First(x => x.Name == "result").GetSetMethod();
                //var resultParam = anyMethodILGenerator.DeclareLocal(hostedMethodInfo.ReturnParameter.ParameterType);
                //var wrappedParam = anyMethodILGenerator.DeclareLocal(responseType);
                //anyMethodILGenerator.Emit(OpCodes.Stloc, resultParam.LocalIndex);
                //anyMethodILGenerator.Emit(OpCodes.Newobj, rtc);
                //anyMethodILGenerator.Emit(OpCodes.Stloc, wrappedParam.LocalIndex);
                //anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                //anyMethodILGenerator.Emit(OpCodes.Ldloc, resultParam.LocalIndex);
                //anyMethodILGenerator.Emit(OpCodes.Call, setMethod);
                //anyMethodILGenerator.Emit(OpCodes.Ldloc, wrappedParam.LocalIndex);
                //Discover Golden End

                var rtc = responseType.GetConstructor(new Type[] { });
                var setResultMethod = responseType.GetProperties().First(x => x.Name == PROP_NAME_RESULT).GetSetMethod();
                var setRCMethod = responseType.GetProperties().First(x => x.Name == PROP_NAME_RC).GetSetMethod();
                var setErrorMethod = responseType.GetProperties().First(x => x.Name == PROP_NAME_ERROR).GetSetMethod();

                LocalBuilder paramResult = anyMethodILGenerator.DeclareLocal(hostedMethodInfo.ReturnParameter.ParameterType);
                LocalBuilder paramException = anyMethodILGenerator.DeclareLocal(typeof(Exception));
                LocalBuilder paramRC = anyMethodILGenerator.DeclareLocal(responseType.GetProperties().First(x => x.Name == PROP_NAME_RC).GetType());
                LocalBuilder paramError = anyMethodILGenerator.DeclareLocal(responseType.GetProperties().First(x => x.Name == PROP_NAME_ERROR).GetType());
                LocalBuilder paramWrapped = anyMethodILGenerator.DeclareLocal(responseType);

                anyMethodILGenerator.Emit(OpCodes.Call, hostedMethodInfo);              //Call the hosted method
                anyMethodILGenerator.Emit(OpCodes.Stloc, paramResult);                  //Store return value from the method call

                anyMethodILGenerator.Emit(OpCodes.Ldc_I4_0);                        
                anyMethodILGenerator.Emit(OpCodes.Stloc, paramRC);                      //Store 0 in the parmRC local variable
                anyMethodILGenerator.Emit(OpCodes.Ldstr, "");                       
                anyMethodILGenerator.Emit(OpCodes.Stloc, paramError);                   //Store value of '' in the errorMessage variable
                anyMethodILGenerator.Emit(OpCodes.Leave, lblExCatch);

                //Begin Catch Block
                anyMethodILGenerator.BeginCatchBlock(typeof(Exception));
                Type exception = typeof(Exception);
                MethodInfo exToStrMI = exception.GetMethod("ToString");

                anyMethodILGenerator.Emit(OpCodes.Stloc, paramException); //Store the Exception object in the local Exception variable
                anyMethodILGenerator.Emit(OpCodes.Ldstr, "Caught {0}");

                anyMethodILGenerator.Emit(OpCodes.Ldloc_S, paramException);
                anyMethodILGenerator.EmitCall(OpCodes.Callvirt, exToStrMI, null);
                anyMethodILGenerator.Emit(OpCodes.Stloc, paramError);

                anyMethodILGenerator.Emit(OpCodes.Ldc_I4_M1);
                anyMethodILGenerator.Emit(OpCodes.Stloc_S, paramRC);
                anyMethodILGenerator.EndExceptionBlock();

                anyMethodILGenerator.Emit(OpCodes.Newobj, rtc);                     //Create new responseType to hold the return value

                //Generic Method
                anyMethodILGenerator.Emit(OpCodes.Stloc, paramWrapped.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramWrapped.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramResult.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Call, setResultMethod);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramWrapped.LocalIndex);

                //RC
                anyMethodILGenerator.Emit(OpCodes.Stloc, paramWrapped.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramWrapped.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramRC.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Call, setRCMethod);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramWrapped.LocalIndex);

                //Error Message
                anyMethodILGenerator.Emit(OpCodes.Stloc, paramWrapped.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramWrapped.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramError.LocalIndex);
                anyMethodILGenerator.Emit(OpCodes.Call, setErrorMethod);
                anyMethodILGenerator.Emit(OpCodes.Ldloc, paramWrapped.LocalIndex);

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
        //private static void CreatePropertyFromParameter(TypeBuilder typeBuilder, ParameterInfo parameterInfo)
        //{
        //    var parameterName = parameterInfo.Name != null ? parameterInfo.Name : "result";
        //    var propertyBuilder =
        //        typeBuilder.DefineProperty(
        //            parameterName, PropertyAttributes.HasDefault, parameterInfo.ParameterType, null);

        //    var fieldBuilder =
        //        typeBuilder.DefineField(
        //            "_" + parameterName, parameterInfo.ParameterType, FieldAttributes.Private);

        //    // Create get method.

        //    var getMethodBuilder =
        //        typeBuilder.DefineMethod(
        //            "get_" + parameterName,
        //            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
        //            parameterInfo.ParameterType,
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
        //Discover Golden End

        private static void CreatePropertyFromParameter(TypeBuilder typeBuilder, ParameterInfo parameterInfo)
        {
            string propertyName = parameterInfo.Name != null ? parameterInfo.Name : PROP_NAME_RESULT;
            CreateProperty(typeBuilder, propertyName, parameterInfo.ParameterType);
        }

        private static void CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
        {
            var propertyBuilder =
                typeBuilder.DefineProperty(
                    propertyName, PropertyAttributes.HasDefault, propertyType, null);

            var fieldBuilder =
                typeBuilder.DefineField(
                    "_" + propertyName, propertyType, FieldAttributes.Private);

            // Create get method.

            var getMethodBuilder =
                typeBuilder.DefineMethod(
                    "get_" + propertyName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    propertyType,
                    Type.EmptyTypes);

            var getMethodILGenerator = getMethodBuilder.GetILGenerator();

            getMethodILGenerator.Emit(OpCodes.Ldarg_0);
            getMethodILGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            getMethodILGenerator.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getMethodBuilder);

            // Create set method

            var setMethodBuilder =
                typeBuilder.DefineMethod(
                    "set_" + propertyName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null,
                    new[] { propertyType });

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

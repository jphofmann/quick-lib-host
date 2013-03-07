using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.Logging.Support.Logging;

namespace QuickHost
{

    public class ServiceStackHost : AppHostHttpListenerBase
    {
        
        private ServiceStackHost(string name, Type[] to_host, Dictionary<string,Type> routeMapping) :
            base("Interface Host: " + name, to_host.ToList().Select( t => t.Assembly ).ToList().Distinct().ToArray() )
        {
            _routeMapping = routeMapping;
        }

        public static ServiceStackHost CreateFrom(string serviceName, object quickHostableClass)
        {
            Dictionary<string, Type> routeMapping;
            return new ServiceStackHost(serviceName, AttributeMapper.MapToServiceStack(quickHostableClass, out routeMapping), routeMapping);   
        }

        private readonly Dictionary<string, Type> _routeMapping;

        public override void Configure(Funq.Container container)
        {
            EndpointHostConfig endpoint_config = new EndpointHostConfig();
            endpoint_config.WsdlSoapActionNamespace = "http://api.quickhost.org/data";
            endpoint_config.WsdlServiceNamespace = "http://api.quickhost.org/data";
            endpoint_config.LogFactory = new DebugLogFactory();
            //endpoint_config.
            SetConfig( endpoint_config );
            foreach (string route in _routeMapping.Keys)
            {
                Routes.Add( _routeMapping[route], "/" + route, "GET");
            }
        }

        // Adapted from http://stackoverflow.com/questions/3862226/dynamically-create-a-class-in-c-sharp
        private class AttributeMapper
        {
            private static Dictionary<string, object> _map = new Dictionary<string, object>();
            private static int map_num = 0;
            public static object mapping(string key)
            {

                return _map.ContainsKey(key) ? _map[key] : null;
            }
            public static Type[] MapToServiceStack(object host_class, out Dictionary<string, Type> name_map)
            {
                _map[map_num.ToString()] = host_class;
                name_map = new Dictionary<string, Type>();
                Type[] mapped_types = null;
                Dictionary<MethodInfo, QuickHostMethodAttribute> methods_to_map = new Dictionary<MethodInfo, QuickHostMethodAttribute>();
                Type t = host_class.GetType();
                foreach (MethodInfo method in t.GetMethods())
                {
                    foreach (object attr in method.GetCustomAttributes(true))
                    {
                        if (attr.GetType() == typeof(QuickHostMethodAttribute))
                        {
                            methods_to_map.Add(method, (QuickHostMethodAttribute)attr);
                            break;
                        }
                    }

                }

                if (methods_to_map.Count != 0)
                {
                    List<Type> type_list = new List<Type>();
                    TypeAttributes type_attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
                        TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout;
                    AssemblyName mapped_name = new AssemblyName("iface_dyn");
                    Type[] string_arg_params = new Type[] { typeof(string) };
                    Type[] no_arg_params = new Type[] { };
                    CustomAttributeBuilder version_attribute = new CustomAttributeBuilder(
                            typeof(AssemblyVersionAttribute).GetConstructor(string_arg_params), new object[] { "1.0.0" });
                    AssemblyBuilder asm_builder = AppDomain.CurrentDomain.DefineDynamicAssembly(mapped_name, AssemblyBuilderAccess.Run);
                    asm_builder.DefineVersionInfoResource("Interface Hoster Dynamic Generator", "1.0.0.0", "Quick Host Group", "(c) 2013 Quick Host Group", null);
                    asm_builder.SetCustomAttribute(version_attribute);
                    ModuleBuilder mod_builder = asm_builder.DefineDynamicModule("InterfaceHost.Dynamic");

                    ConstructorInfo datacontract_attr = typeof(System.Runtime.Serialization.DataContractAttribute).GetConstructor(no_arg_params);
                    PropertyInfo datacontract_namespace = typeof(System.Runtime.Serialization.DataContractAttribute).GetProperty("Namespace");
                    ConstructorInfo datamember_attr = typeof(System.Runtime.Serialization.DataMemberAttribute).GetConstructor(no_arg_params);
                    CustomAttributeBuilder dc_attr_builder = new CustomAttributeBuilder(datacontract_attr, new object[] { },
                        new PropertyInfo[] { datacontract_namespace }, new object[] { "http://api.quickhost.org/data" });
                    CustomAttributeBuilder dm_attr_builder = new CustomAttributeBuilder(datamember_attr, new object[] { });

                    foreach (MethodInfo mi in methods_to_map.Keys)
                    {
                        string method = methods_to_map[mi].MethodAlias;
                        TypeBuilder servicebuilder = mod_builder.DefineType("InterfaceHost.Dynamic." + /*host_class.HostedShortName +*/ "." + method + "Server", type_attributes,
                        typeof(ServiceStack.ServiceInterface.Service));
                        TypeBuilder inputbuilder = mod_builder.DefineType("InterfaceHost.Dynamic." + /*host_class.HostedShortName +*/ "." + method, type_attributes, null);
                        TypeBuilder outputbuilder = mod_builder.DefineType("InterfaceHost.Dynamic." + /*host_class.HostedShortName +*/ "." + method + "Response", type_attributes, null);
                        ConstructorBuilder svc_ctor_builder = servicebuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                        ConstructorBuilder in_ctor_builder = inputbuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                        ConstructorBuilder out_ctor_builder = outputbuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                        inputbuilder.SetCustomAttribute(dc_attr_builder);
                        outputbuilder.SetCustomAttribute(dc_attr_builder);

                        // map arguments to input function to input class.

                        foreach (ParameterInfo param in mi.GetParameters())
                        {
                            CreateProperty(inputbuilder, param.Name, param.ParameterType, dm_attr_builder);
                        }
                        Type input = inputbuilder.CreateType();
                        Type output = outputbuilder.CreateType();
                        Object input_object = input.GetConstructor(new Type[] { }).Invoke(new object[] { });


                        MethodBuilder mth_builder = servicebuilder.DefineMethod("Any", MethodAttributes.Public, typeof(object), new Type[] { input });
                        ILGenerator il_gen = mth_builder.GetILGenerator();
                        MethodInfo console_debug_out = typeof(System.Console).GetMethod("WriteLine", new Type[] { typeof(string) });
                        MethodInfo mapping_func = typeof(AttributeMapper).GetMethod("mapping");
                        List<LocalBuilder> local_args = new List<LocalBuilder>();


                        foreach (ParameterInfo param in mi.GetParameters())
                        {
                            PropertyInfo assignee = null;
                            foreach (PropertyInfo prop in input.GetProperties())
                            {
                                if (prop.Name == param.Name)
                                {
                                    assignee = prop;
                                    break;
                                }
                            }
                            LocalBuilder loc_input = il_gen.DeclareLocal(param.ParameterType);
                            il_gen.Emit(OpCodes.Ldarg_1);
                            il_gen.Emit(OpCodes.Call, assignee.GetGetMethod());
                            il_gen.Emit(OpCodes.Stloc, loc_input.LocalIndex);
                            local_args.Add(loc_input);
                        }
                        il_gen.Emit(OpCodes.Ldstr, map_num.ToString());
                        il_gen.Emit(OpCodes.Call, mapping_func);
                        foreach (LocalBuilder loc in local_args)
                            il_gen.Emit(OpCodes.Ldloc, loc.LocalIndex);
                        il_gen.EmitWriteLine("Calling " + mi.Name + ".");
                        il_gen.Emit(OpCodes.Call, mi);
                        il_gen.Emit(OpCodes.Ret);

                        Type service = servicebuilder.CreateType();
                        type_list.Add(service);

                        if (methods_to_map[mi].RestUriAlias != null)
                        {
                            // produce /func/{Arg1}/{Arg2} style uri
                            string uri = methods_to_map[mi].RestUriAlias + mi.GetParameters().Aggregate<ParameterInfo, string>("", (sd, pi) => sd += "/{" + pi.Name + "}");
                            name_map[uri] = input;
                        }
                    }
                    mapped_types = type_list.ToArray();
                }

                map_num++;
                return mapped_types;
            }

            private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType, CustomAttributeBuilder attr_to_attach)
            {
                string propertyNameNamespace = "";
                FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

                PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
                MethodBuilder getPropMethodBuilder = tb.DefineMethod("get_" + propertyNameNamespace + propertyName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    propertyType, Type.EmptyTypes);
                ILGenerator getMIl = getPropMethodBuilder.GetILGenerator();

                getMIl.Emit(OpCodes.Ldarg_0);
                // getMIl.EmitWriteLine("Getting " + propertyName);
                getMIl.Emit(OpCodes.Ldfld, fieldBuilder);
                getMIl.Emit(OpCodes.Ret);

                MethodBuilder setPropMethodBuilder =
                    tb.DefineMethod("set_" + propertyNameNamespace + propertyName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null, new[] { propertyType });

                ILGenerator setMil = setPropMethodBuilder.GetILGenerator();
                Label modifyProperty = setMil.DefineLabel();
                Label exitSet = setMil.DefineLabel();

                setMil.MarkLabel(modifyProperty);
                setMil.Emit(OpCodes.Ldarg_0);
                setMil.Emit(OpCodes.Ldarg_1);
                setMil.Emit(OpCodes.Stfld, fieldBuilder);

                setMil.Emit(OpCodes.Nop);
                setMil.MarkLabel(exitSet);
                setMil.Emit(OpCodes.Ret);

                propertyBuilder.SetGetMethod(getPropMethodBuilder);
                propertyBuilder.SetSetMethod(setPropMethodBuilder);

                if (attr_to_attach != null)
                    propertyBuilder.SetCustomAttribute(attr_to_attach);


            }
        }
    }

    
}

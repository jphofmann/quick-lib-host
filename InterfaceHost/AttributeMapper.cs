using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OldQuick.Inventory;
using System.Reflection;
using System.Reflection.Emit;

namespace InterfaceHost
{
    // Adapted from http://stackoverflow.com/questions/3862226/dynamically-create-a-class-in-c-sharp
    public class AttributeMapper
    {
        private static Dictionary<string, OldQuickInventoryHost> _map = new Dictionary<string, OldQuickInventoryHost>();
        private static int map_num = 0;
        public static OldQuickInventoryHost mapping(string key)
        {

            return _map.ContainsKey(key) ? _map[key] : null ;
        }
        public static Type[] MapToServiceStack(OldQuickInventoryHost host_class, out Dictionary<string, Type> name_map)
        {
            _map[map_num.ToString()] = host_class;
            name_map = new Dictionary<string, Type>();
            Type[] mapped_types = null;
            Dictionary<MethodInfo, HostInterfaceMethodAttribute> methods_to_map = new Dictionary<MethodInfo, HostInterfaceMethodAttribute>();
            Type t = host_class.GetType();
            foreach (MethodInfo method in t.GetMethods())
            {
                foreach (object attr in method.GetCustomAttributes(true))
                {
                    if (attr.GetType() == typeof(HostInterfaceMethodAttribute))
                    {
                        methods_to_map.Add(method, (HostInterfaceMethodAttribute)attr);
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
                CustomAttributeBuilder dc_attr_builder = new CustomAttributeBuilder(datacontract_attr, new object[] {},
                    new PropertyInfo[] {  datacontract_namespace },  new object[] {  "http://api.quickhost.org/data" });
                CustomAttributeBuilder dm_attr_builder = new CustomAttributeBuilder(datamember_attr, new object[] { });

                foreach (MethodInfo mi in methods_to_map.Keys)
                {
                    string method = methods_to_map[mi].MethodName;
                    TypeBuilder servicebuilder = mod_builder.DefineType("InterfaceHost.Dynamic." + host_class.HostedShortName + "." + method + "Server", type_attributes,
                    typeof(ServiceStack.ServiceInterface.Service));
                    //CreateProperty(servicebuilder, "real", typeof(OldQuickInventoryHost), null);
                    TypeBuilder inputbuilder = mod_builder.DefineType("InterfaceHost.Dynamic." + host_class.HostedShortName + "." + method, type_attributes, null);
                    TypeBuilder outputbuilder = mod_builder.DefineType("InterfaceHost.Dynamic." + host_class.HostedShortName + "." + method + "Response", type_attributes, null);
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


                    MethodBuilder mth_builder = servicebuilder.DefineMethod("Any", MethodAttributes.Public, typeof(object), new Type[] {input });
                    //ConstructorInfo ss_attribute = 
                    CustomAttributeBuilder x;
                    //mth_builder.SetCustomAttribute();
                    ILGenerator il_gen = mth_builder.GetILGenerator();
                    MethodInfo console_debug_out = typeof(System.Console).GetMethod("WriteLine", new Type[] { typeof(string) });
                    MethodInfo mapping_func = typeof(AttributeMapper).GetMethod("mapping");
                    //ConstructorInfo class_cons = typeof(
                    List<LocalBuilder> local_args = new List<LocalBuilder>();


                    il_gen.Emit(OpCodes.Ldarg_1);
                    foreach( ParameterInfo param in mi.GetParameters())
                    {
                        PropertyInfo assignee = null;
                        PropertyInfo[] fin = input.GetProperties( ); //BindingFlags.NonPublic );
                        foreach( PropertyInfo prop in input.GetProperties( ))
                        {
                            if( prop.Name == param.Name )
                            {
                                assignee = prop;
                                break;
                            }
                        }
                        LocalBuilder loc_input = il_gen.DeclareLocal( param.ParameterType );
                        il_gen.Emit(OpCodes.Call, assignee.GetGetMethod());
                        il_gen.Emit(OpCodes.Stloc, loc_input.LocalIndex);

                        //il_gen.EmitWriteLine("Loading argument " );
                        //il_gen.EmitWriteLine(loc_input);
                        //il_gen.EmitWriteLine("Blarg?" + loc_input.LocalType.ToString());
                        //il_gen.EmitWriteLine("Done");
                        local_args.Add(loc_input);
                    }
                    il_gen.Emit(OpCodes.Pop);
                    il_gen.Emit(OpCodes.Ldstr, map_num.ToString());
                    il_gen.Emit(OpCodes.Call,mapping_func);
                    //foreach (LocalBuilder loc in local_args)
                    //    il_gen.Emit(OpCodes.Ldloc, loc.LocalIndex);

                    //il_gen.EmitWriteLine("All Arugments Loaded");
                    //il_gen.Emit(OpCodes.Newobj,);
                    //il_gen.Emit(OpCodes.Call, mi);

                    //il_gen.Emit(OpCodes.Ldstr, "Moo");
                    il_gen.Emit(OpCodes.Ret);

                    //il_gen.Emit(OpCodes.Ldstr, "Testing " + method);
                    //il_gen.Emit(OpCodes.Ldarg_0);
                    //il_gen.Emit(OpCodes.Call, console_debug_out);
                    //il_gen.Emit(OpCodes.Ldarg_0);
                    //il_gen.Emit(OpCodes.Nop);
                    //il_gen.EmitWriteLine("At end of " + method);
                    //il_gen.Emit(OpCodes.Ldstr, "Testing Again");
                    //il_gen.Emit(OpCodes.Stloc,);

                    string msig = mth_builder.Signature;

                    Type service = servicebuilder.CreateType();
                    type_list.Add(service);

                    if (methods_to_map[mi].ShortName != null)
                    {

                        // produce /func/{Arg1}/{Arg2} style uri
                        string uri = methods_to_map[mi].ShortName + mi.GetParameters().Aggregate<ParameterInfo, string>("", (sd, pi) => sd += "/{" + pi.Name + "}");
                        name_map[uri] = input;
                    }

                    //Func<int, object> testo;
                    //testo = (Func<int,object>)mth_builder.

                    // test using "InventoryExists" from Main.
                    Object p = input.GetConstructor(new Type[] { }).Invoke(new object[] { });
                    PropertyInfo[] pis = input.GetProperties();
                    input.GetProperty("id").GetSetMethod().Invoke( p, new object[] { 42 } );
                    Object o = service.GetConstructor(new Type[] { }).Invoke(new object[] { });
                    //service.InvokeMember("set_real", BindingFlags.Public | BindingFlags.SetProperty, null, o, new object[] { host_class });
                    MethodInfo[] mis = service.GetMethods();
                    object anyout = service.InvokeMember("Any", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, o, new object[] { p });

                }
                mapped_types = type_list.ToArray();

            }


            map_num++;
            return mapped_types;
        }

        private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType, CustomAttributeBuilder attr_to_attach )
        {
            string propertyNameNamespace = "";
            FieldBuilder fieldBuilder = tb.DefineField("_"+ propertyName, propertyType, FieldAttributes.Private);

            PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            MethodBuilder getPropMethodBuilder = tb.DefineMethod("get_" + propertyNameNamespace  + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType, Type.EmptyTypes);
            ILGenerator getMIl = getPropMethodBuilder.GetILGenerator();

            getMIl.Emit(OpCodes.Ldarg_0);
            getMIl.EmitWriteLine("Getting " + propertyName);
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

            if( attr_to_attach != null )
                propertyBuilder.SetCustomAttribute(attr_to_attach);


        }
    }
}

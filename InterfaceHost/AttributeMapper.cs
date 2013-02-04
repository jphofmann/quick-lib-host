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
    class AttributeMapper
    {
        public static Type[] MapToServiceStack(OldQuickInventoryHost host_class)
        {
            Type[] mapped_types = null;
            Dictionary<MethodInfo,HostInterfaceMethodAttribute> methods_to_map = new Dictionary<MethodInfo,HostInterfaceMethodAttribute>();
            Type t = host_class.GetType();
            foreach( MethodInfo method in t.GetMethods())
            {
                foreach (object attr in method.GetCustomAttributes( true ))
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
                //new List<CustomAttributeBuilder>( new CustomAttributeBuilder[] { new AssemblyVersionBuilder( "1.0.0.0") });
                CustomAttributeBuilder version_attribute = new CustomAttributeBuilder(
                        typeof(AssemblyVersionAttribute).GetConstructor( string_arg_params ), new object[] { "1.0.0"});
                AssemblyBuilder asm_builder = AppDomain.CurrentDomain.DefineDynamicAssembly( mapped_name, AssemblyBuilderAccess.Run);
                asm_builder.DefineVersionInfoResource("Interface Hoster Dynamic Generator", "1.0.0.0", "Quick Host Group", "(c) 2013 Quick Host Group", null);
                asm_builder.SetCustomAttribute(version_attribute);
                ModuleBuilder mod_builder = asm_builder.DefineDynamicModule("InterfaceHost.Dynamic");

                foreach (MethodInfo mi in methods_to_map.Keys)
                {
                    string method = methods_to_map[mi].MethodName;
                    TypeBuilder servicebuilder = mod_builder.DefineType(  "InterfaceHost.Dynamic." + host_class.HostedShortName +"."+ method +"Service"  , type_attributes,
                    typeof(ServiceStack.ServiceInterface.Service) );
                    TypeBuilder inputbuilder = mod_builder.DefineType(  "InterfaceHost.Dynamic." + host_class.HostedShortName +"."+ method   , type_attributes,null);
                    TypeBuilder outputbuilder = mod_builder.DefineType(  "InterfaceHost.Dynamic." + host_class.HostedShortName +"."+ method +"Response"  , type_attributes,null);
                    ConstructorBuilder svc_ctor_builder = servicebuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                    ConstructorBuilder in_ctor_builder = inputbuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                    ConstructorBuilder out_ctor_builder = outputbuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                    ConstructorInfo datacontract_attr = typeof(System.Runtime.Serialization.DataContractAttribute).GetConstructor( string_arg_params);
                    ConstructorInfo datamember_attr = typeof(System.Runtime.Serialization.DataMemberAttribute).GetConstructor( no_arg_params);
                    CustomAttributeBuilder dc_attr_builder = new CustomAttributeBuilder(datacontract_attr, new object[] { "http://api.quickhost.org/data" });
                    CustomAttributeBuilder dm_attr_builder = new CustomAttributeBuilder(datamember_attr, new object[] {} );
                    inputbuilder.SetCustomAttribute(dc_attr_builder);
                    outputbuilder.SetCustomAttribute(dc_attr_builder);

                    // map arguments to input function to input class.

                    foreach (ParameterInfo param in mi.GetParameters())
                    {
                        FieldBuilder field =  CreateProperty( inputbuilder, param.Name, param.ParameterType );
                        field.SetCustomAttribute( dm_attr_builder );
                    }


                    MethodBuilder mth_builder = servicebuilder.DefineMethod("Any", MethodAttributes.Public,typeof(object), new Type[] { inputbuilder.CreateType() });
                    //ConstructorInfo ss_attribute = 
                    CustomAttributeBuilder x;
                    //mth_builder.SetCustomAttribute();
                    ILGenerator il_gen = mth_builder.GetILGenerator();

                    int count = mi.GetParameters().Count();
                    for (int i = 0; i < count; i++)
                        il_gen.Emit(OpCodes.Ldarg, (byte)i);
                    il_gen.Emit( OpCodes.Call, mi);
                    il_gen.Emit(OpCodes.Ret);
                    type_list.Add(  servicebuilder.CreateType());
                }
                mapped_types = type_list.ToArray();

            }


            return mapped_types;
        }

        private static FieldBuilder CreateProperty( TypeBuilder tb, string propertyName, Type propertyType )
        {
            FieldBuilder fieldBuilder = tb.DefineField("_"+propertyName, propertyType, FieldAttributes.Private );

            PropertyBuilder propertyBuilder = tb.DefineProperty( propertyName, PropertyAttributes.HasDefault, propertyType, null );
            MethodBuilder getPropMethodBuilder = tb.DefineMethod( "get_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType, Type.EmptyTypes );
            ILGenerator getMIl = getPropMethodBuilder.GetILGenerator();

            getMIl.Emit( OpCodes.Ldarg_0);
            getMIl.Emit(OpCodes.Ldfld,fieldBuilder);
            getMIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMethodBuilder = 
                tb.DefineMethod( "set_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null, new[] { propertyType } );

            ILGenerator setMil = setPropMethodBuilder.GetILGenerator();
            Label modifyProperty = setMil.DefineLabel();
            Label exitSet = setMil.DefineLabel();

            setMil.MarkLabel(modifyProperty);
            setMil.Emit(OpCodes.Ldarg_0);
            setMil.Emit(OpCodes.Ldarg_1);
            setMil.Emit(OpCodes.Stfld,fieldBuilder);

            setMil.Emit(OpCodes.Nop);
            setMil.MarkLabel(exitSet);
            setMil.Emit(OpCodes.Ret);

            return fieldBuilder;

    }
}

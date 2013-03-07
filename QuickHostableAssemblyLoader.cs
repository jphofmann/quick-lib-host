using System;
using System.Reflection;
using System.IO;
using System.Security;

namespace QuickHost
{
    class QuickHostableAssemblyLoader
    {
        public static void Load(string assemblyPath, out object quickHostableClass, out string serviceName)
        {
            quickHostableClass = null;
            serviceName = null;
            
            Assembly quickHostableAssembly;
            
            try
            {
                quickHostableAssembly = Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception e)
            {
                throw new Exception(String.Format("Could not load {0} for hosting.", assemblyPath), e);
            }

            Type quickHostableType = null;

            foreach (var assemblyType in quickHostableAssembly.GetTypes())
            {
                var attributes = assemblyType.GetCustomAttributes(typeof (QuickHostableAttribute), true);
                
                if (attributes.Length == 0) 
                    continue;

                serviceName = ((QuickHostableAttribute) attributes[0]).ServiceName;
                quickHostableType = assemblyType;
                break;
            }

            if (quickHostableType == null)
            {
                throw new Exception(
                    String.Format(
                        "Load of {0} failed. No class with QuickHostable attribute found.",
                        assemblyPath));
            }

            var constructorInfo = quickHostableType.GetConstructor(new Type[] {});

            if (constructorInfo != null)
            {
                quickHostableClass = constructorInfo.Invoke(null);
            }
            else
            {
                throw new Exception(
                    String.Format(
                        "Load of {0} from {1} failed. No suitable constructor found.",
                        quickHostableType.Name,
                        assemblyPath));
            }
        }
    }
}

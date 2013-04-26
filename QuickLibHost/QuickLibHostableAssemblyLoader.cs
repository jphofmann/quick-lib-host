using System;
using System.Reflection;
using System.IO;
using System.Security;

namespace QuickLibHost
{
    class QuickLibHostableAssemblyLoader
    {
        public static void Load(string assemblyPath, out object quickLibHostableClass, out string serviceName)
        {
            quickLibHostableClass = null;
            serviceName = null;
            
            Assembly quickLibHostableAssembly;
            
            try
            {
                quickLibHostableAssembly = Assembly.LoadFrom(assemblyPath);

                AppDomain.CurrentDomain.AssemblyResolve +=
                    (sender, args) =>
                        {
                            var path =
                                Path.Combine(assemblyPath.Substring(0, assemblyPath.LastIndexOf('\\') + 1) + args.Name + ".dll");
                            
                            if (!File.Exists(path))
                                return null;
                            
                            return Assembly.LoadFrom(path);
                        };
            }
            catch (Exception e)
            {
                throw new Exception(String.Format("Could not load {0} for hosting.", assemblyPath), e);
            }

            Type quickLibHostableType = null;

            foreach (var assemblyType in quickLibHostableAssembly.GetTypes())
            {
                var attributes = assemblyType.GetCustomAttributes(typeof (QuickLibHostableAttribute), true);
                
                if (attributes.Length == 0) 
                    continue;

                serviceName = ((QuickLibHostableAttribute) attributes[0]).ServiceName;
                quickLibHostableType = assemblyType;
                break;
            }

            if (quickLibHostableType == null)
            {
                throw new Exception(
                    String.Format(
                        "Load of {0} failed. No class with QuickLibHostable attribute found.",
                        assemblyPath));
            }

            var constructorInfo = quickLibHostableType.GetConstructor(new Type[] {});

            if (constructorInfo != null)
            {
                quickLibHostableClass = constructorInfo.Invoke(null);
            }
            else
            {
                throw new Exception(
                    String.Format(
                        "Load of {0} from {1} failed. No suitable constructor found.",
                        quickLibHostableType.Name,
                        assemblyPath));
            }
        }
    }
}

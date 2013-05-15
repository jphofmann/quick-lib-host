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

using System;
using System.Reflection;
using System.IO;
using QuickLibHostClient;

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

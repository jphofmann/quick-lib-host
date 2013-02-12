using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Security;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Diagnostics;
using System.Xml;
using System.Xml.XPath;
using OldQuick.Inventory;

namespace InterfaceHost
{
    public class Program
    {
        static void Main(string[] args)
        {
            DllLoader loader = new DllLoader(args[0]);
            OldQuickInventoryRepositoryHostable h = loader.host.GetConstructor(new Type[] { }).Invoke(null) as OldQuickInventoryRepositoryHostable;
            Dictionary<string, Type> shortname_map;
            string service_name = h.GetName();
            Type[] mapped_types = AttributeMapper.MapToServiceStack(h.GetHost(), out shortname_map);
            ServiceStackHost ssh = new ServiceStackHost( service_name, mapped_types, shortname_map );
            try
            {
                ssh.Init();
            }
            catch (Exception e)
            {
                Exception ie = e.InnerException;
                if (ie is ReflectionTypeLoadException)
                {
                    Exception[] le = ((ReflectionTypeLoadException)ie).LoaderExceptions;
                }
            }
            ssh.Start("http://+:8088/joinus/");
            Console.WriteLine("ServiceStack-based InterfaceHost v0.0.1, Serving " + service_name);
            Console.WriteLine("Press <enter> to terminate.");
            Console.ReadLine();
        }

    }
}

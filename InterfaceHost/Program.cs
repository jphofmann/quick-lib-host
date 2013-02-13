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
    // Because ServiceStack uses the .NET Web Hosting code on windows, 
    // this may require a netssh http add urlacl url=http://+:8080/my_dll/ user=\Everyone by an Administrator
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() != 2)
            {
                Console.WriteLine("InterfaceHosh: argument error. Format is <dll> <hosting_url>");
                Console.WriteLine("dll can be relative or absolute path, hosting_url is MS hosting url style ie: http://+:88/my_dll/");
                Environment.Exit(1);
            }
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
                    Console.WriteLine("Error loading dll for hosting: : " + e.ToString());
                    Console.WriteLine("Loader exceptions are:" + le.Select( ile => ile.ToString()).Aggregate( (f,s) => f + "\n" + s ));
                }
                else
                {
                    Console.WriteLine("Error while initializing hosting: " + e.ToString());
                }
                Environment.Exit(2);
            }
            try
            {
                ssh.Start(args[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error starting Hosting: " + e.ToString());
                Environment.Exit(3);
            }
            Console.WriteLine("ServiceStack-based InterfaceHost v0.0.1, Serving " + service_name);
            Console.WriteLine("Press <enter> to terminate.");
            Console.ReadLine();
            Environment.Exit(0);
        }

    }
}

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

namespace QuickHost
{
    // Because ServiceStack uses the .NET Web Hosting code on windows, 
    // this may require a netsh http add urlacl url=http://+:8080/my_dll/ user=\Everyone by an Administrator
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() != 2)
            {
                Console.WriteLine("QuickHost: argument error. Format is <assembly_path> <hosting_url>");
                Console.WriteLine("<assembly_path> can be a relative or absolute path.");
                Console.WriteLine("<hosting_url> is a MS hosting style url. (ie: http://+:88/my_dll/)");
                Console.ReadLine();
                Environment.Exit(1);
            }

            object hostedDll = null;
            string serviceName = null;

            try
            {
                QuickHostableAssemblyLoader.Load(args[0], out hostedDll, out serviceName);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading assembly." + e);
                Console.ReadLine();
                Environment.Exit(2);
            }

            var shortname_map = new Dictionary<string,Type>();

            var mapped_types = AttributeMapper.MapToServiceStack(hostedDll, out shortname_map);

            var serviceStackHost = new ServiceStackHost(serviceName, mapped_types, shortname_map);
            
            //try
            //{
                serviceStackHost.Init();
            /*
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

                Console.ReadLine();
                Environment.Exit(3);
            }
            */

            //try
            //{
                serviceStackHost.Start(args[1]);
            /*    
            }
            catch (Exception e)
            {
                Console.WriteLine("Error starting Hosting: " + e.ToString());
                Console.ReadLine();
                Environment.Exit(4);
            }
            */

            Console.WriteLine("ServiceStack-based InterfaceHost v0.0.1, Serving {0} @ {1}", serviceName, args[1]);
            Console.WriteLine("Press <enter> to terminate.");
            Console.ReadLine();
            Environment.Exit(0);
        }

    }
}

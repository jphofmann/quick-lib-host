using System;
using System.Linq;
using System.Reflection;

namespace QuickLibHost
{
    // Because ServiceStack uses the .NET Web Hosting code on windows, 
    // this may require a netsh http add urlacl url=http://+:8080/my_dll/ user=\Everyone by an Administrator
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() != 2)
            {
                Console.WriteLine("QuickLibHost: argument error. Format is <assembly_path> <hosting_url>");
                Console.WriteLine("<assembly_path> can be a relative or absolute path.");
                Console.WriteLine("<hosting_url> is a MS hosting style url. (ie: http://+:88/my_dll/)");
                Console.ReadLine();
                Environment.Exit(1);
            }

            object quickLibHostableClass = null;
            string serviceName = null;

            try
            {
                QuickLibHostableAssemblyLoader.Load(args[0], out quickLibHostableClass, out serviceName);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading assembly. " + e);
                Console.ReadLine();
                Environment.Exit(2);
            }

            ServiceStackHost serviceStackHost = null;

            try
            {
                serviceStackHost = ServiceStackHost.CreateFrom(serviceName, quickLibHostableClass);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error creating ServiceStackHost. " + e);
                Console.ReadLine();
                Environment.Exit(3);
            }

            try
            {
                serviceStackHost.Init();
            }
            catch (Exception e)
            {
                var exception = e.InnerException as ReflectionTypeLoadException;
                if (exception != null)
                {
                    Console.WriteLine("Error loading Assembly for hosting: " + e);
                    Console.WriteLine("Loader exceptions are:" + exception.LoaderExceptions.Select( ile => ile.ToString()).Aggregate( (f,s) => f + "\n" + s ));
                }
                else
                {
                    Console.WriteLine("Error while initializing hosting: " + e);
                }

                Console.ReadLine();
                Environment.Exit(4);
            }
            
            try
            {
                serviceStackHost.Start(args[1]);
                
            }
            catch (Exception e)
            {
                Console.WriteLine("Error starting Hosting: " + e);
                Console.ReadLine();
                Environment.Exit(5);
            }
            
            Console.WriteLine("ServiceStack-based InterfaceHost v0.0.1, Serving {0} @ {1}", serviceName, args[1]);
            Console.WriteLine("Press <enter> to terminate.");
            Console.ReadLine();
            Environment.Exit(0);
        }

    }
}

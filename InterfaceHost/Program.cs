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

namespace InterfaceHost
{
    public class Program
    {
        static Assembly host_library = null;
        static OldQuick.Inventory.IOldQuickInventoryRepository host_repo = null;
        static void Main(string[] args)
        {
            string dll = "MAIN.DLL";
            string service_name = "Main Inventory"; // read from dll.
            ServiceStackHost ssh = new ServiceStackHost( service_name );
            ssh.Init();
            //ssh.Start("http://192.168.4.57:9000/");
            ssh.Start("http://+:8088/joinus/");
            Console.WriteLine("ServiceStack-based InterfaceHost v0.0.1, Serving " + service_name);
            Console.WriteLine("Press <enter> to terminate.");
            Console.ReadLine();
        }

    }
}

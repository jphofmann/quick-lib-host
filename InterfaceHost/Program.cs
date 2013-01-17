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
    class Program
    {
        static Assembly host_library = null;
        static OldQuick.Inventory.IOldQuickInventoryRepository host_repo = null;
        static void Main(string[] args)
        {
            if (args.Count() < 1)
            {
                System.Console.WriteLine("Library to serve required.");
                System.Environment.Exit(1);
            }

            bool load_ok = false;

            try
            {
                host_library = Assembly.LoadFrom(args[0]);
                load_ok = true;
            }
            catch (FileNotFoundException fnf)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. File not found (or possibly not openable by this process. Error is: {1}", args[0], fnf.ToString()));
            }
            catch (FileLoadException fl)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. Could not open file, that exists. Error is: {1}", args[0], fl.ToString()));
            }
            catch (BadImageFormatException bif)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. Is this a DLL, for this platform? Error is: {1}", args[0], bif.ToString()));
            }
            catch (SecurityException se)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. A Security Exception Occurred: {1}", args[0], se.ToString()));
            }
            catch (Exception all_others)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. No specific help for this error: {1}", args[0], all_others.ToString()));
            }

            if (!load_ok)
            {
                System.Environment.Exit(2);
            }


            load_ok = false;
            Type hosting_type = null;

            foreach( Type assembly_type in host_library.GetTypes() )
            {
                if (assembly_type.BaseType == typeof(OldQuick.Inventory.OldQuickInventoryRepositoryHostable))
                {
                    hosting_type = assembly_type;
                    break;
                }
                else
                {
                    Type[] all = assembly_type.GetInterfaces();
                }
            }


            if (hosting_type == null)
            {
                System.Environment.Exit(3);
            }
            OldQuick.Inventory.OldQuickInventoryRepositoryHostable hosting_bootstrap = 
            hosting_type.GetConstructor(new Type[] { }).Invoke(null) as OldQuick.Inventory.OldQuickInventoryRepositoryHostable;
            host_repo = hosting_bootstrap.GetRepository();
            object happy_object = hosting_bootstrap.GetHostable();
            Uri uri = new Uri("http://192.168.4.57:8088/joinus");
            using (ServiceHost host = new ServiceHost(happy_object, uri))
            {
               string USER_DIR = System.Environment.GetEnvironmentVariable("USERPROFILE");
                //XmlWriterTraceListener debug_out = new XmlWriterTraceListener( Path.Combine( USER_DIR , "Trace.log"));
                //XmlWriterTraceListener wcf_out = new XmlWriterTraceListener( Path.Combine( USER_DIR , "Message_Trace.log"));
                //ConsoleTraceListener console_out = new ConsoleTraceListener();
                //TraceSource trace_hook = new TraceSource("Service_Trace");
                //TraceSource message_logger = new TraceSource("System.ServiceModel");
                //TraceSource message_logger = new TraceSource("System.ServiceModel.MessageLogging");
                //message_logger.Listeners.Add(wcf_out);
                //message_logger.Listeners.Add(console_out);
                //message_logger.Switch.Level = SourceLevels.All;
                //trace_hook.Listeners.Add(debug_out);
                //trace_hook.Listeners.Add(console_out);
                //trace_hook.Switch.Level = SourceLevels.All;


                //string startXml = "<AppStart><App Name=\"InterfaceHost\" Version=\"0.1\"/><TimeStamp>" + DateTime.Now.ToString() + "</TimeStamp></AppStart>";
                //XmlTextReader xmlReader = new XmlTextReader(new StringReader(startXml));
                //XPathDocument xDoc = new XPathDocument(xmlReader);
                //trace_hook.TraceData(TraceEventType.Information, 1, xDoc.CreateNavigator() );
                //trace_hook.TraceData(TraceEventType.Information, 1, "Application Start: InterfaceHost, 0.1");
                //trace_hook.Flush();
                TraceListener listner = RebindTrace();
                ServiceMetadataBehavior service_behavior = new ServiceMetadataBehavior();
                ServiceDebugBehavior debug_behavior = new ServiceDebugBehavior();
                debug_behavior.HttpHelpPageEnabled = true;
                debug_behavior.IncludeExceptionDetailInFaults = true;
                 service_behavior.HttpGetEnabled = true;
                 //Binding get_binding = new WSHttpBinding();
                 Binding get_binding = new WebHttpBinding();
                 get_binding.Name = "Getter";
                 Binding x = new WebHttpBinding();
                 x.Name = "YY";
                 service_behavior.HttpGetBinding = x;
                //service_behavior.HttpGetBinding = new WebHttpBinding("get_binding"); //, "http://internal.quick.com");
                 //service_behavior.HttpGetBinding = get_binding;
                service_behavior.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
                host.Description.Behaviors.Add(service_behavior);
                host.Description.Behaviors.Add( new WCFMessageAttribute());
                host.Open();
                Console.WriteLine("Press <enter> to terminate.");
                Console.ReadLine();
                Console.WriteLine("Exiting.");
                host.Close();
                listner.Close();
                //trace_hook.Close();
                //message_logger.Flush();
                //message_logger.Close();
            }
//                OldQuick.Inventory.IOldQuickInventoryRepositoryHostable host_han



            ServiceContractAttribute sca = new ServiceContractAttribute();
            




        }

        private static TraceListener RebindTrace()
        {
            TraceListener l = null;
            BindingFlags privateMemberFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            BindingFlags privateStaticMemberFlags = privateMemberFlags | BindingFlags.Static;
            Type diagUtilityType = Type.GetType("System.ServiceModel.DiagnosticUtility, System.ServiceModel, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            MethodInfo[] allMethods = diagUtilityType.GetMethods(privateStaticMemberFlags);
            object diagnosticTrace = allMethods.FirstOrDefault(method => method.Name == "InitializeTracing").Invoke(null, null);
            if (diagnosticTrace != null)
            {
                // get Trace Source
                Type diagTraceType = Type.GetType("System.ServiceModel.Diagnostics.DiagnosticTrace, SMDiagnostics, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                PropertyInfo diagTypeSourceProperty = diagTraceType.GetProperty("TraceSource", privateMemberFlags);
                TraceSource traceSource = diagTypeSourceProperty.GetValue(diagnosticTrace, null) as TraceSource;

                // clear all listners (ie, the Dummy )
                traceSource.Listeners.Clear();

                string USER_DIR = System.Environment.GetEnvironmentVariable("USERPROFILE");
                XmlWriterTraceListener debug_out = new XmlWriterTraceListener(Path.Combine(USER_DIR, "WCF_Trace.log"));
                ConsoleTraceListener console_out = new ConsoleTraceListener();
                debug_out.TraceOutputOptions = TraceOptions.DateTime; // | TraceOptions.Callstack;
                traceSource.Attributes["propagateActivity"] = "true";
                //traceSource.Switch.ShouldTrace(TraceEventType.Verbose | TraceEventType.Start);
                traceSource.Switch.Level = SourceLevels.All;
                traceSource.Switch.ShouldTrace(TraceEventType.Verbose | TraceEventType.Start| TraceEventType.Information);
                traceSource.Listeners.Add(debug_out);
                traceSource.Listeners.Add(console_out);
                Trace.AutoFlush = true;
                l = debug_out;

            }
            return l;
        }

        private static bool filter_func(Type t, object filter_info)
        {
            return false;
        }
    }
}

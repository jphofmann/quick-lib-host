using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Security;

namespace InterfaceHost
{
    class DllLoader
    {
        public Type host { get { return _host; } }
        private Type _host;
        public Assembly dll { get { return _dll; } }
        private Assembly _dll;

        public DllLoader(string path_to_load)
        {
            _dll = null;
            bool load_ok = false;

            try
            {
                _dll = Assembly.LoadFrom(path_to_load);
                load_ok = true;
            }
            catch (FileNotFoundException fnf)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. File not found (or possibly not openable by this process. Error is: {1}", path_to_load, fnf.ToString()));
            }
            catch (FileLoadException fl)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. Could not open file, that exists. Error is: {1}", path_to_load, fl.ToString()));
            }
            catch (BadImageFormatException bif)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. Is this a DLL, for this platform? Error is: {1}", path_to_load, bif.ToString()));
            }
            catch (SecurityException se)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. A Security Exception Occurred: {1}", path_to_load, se.ToString()));
            }
            catch (Exception all_others)
            {
                System.Console.WriteLine(String.Format("Could not load {0} for hosting. No specific help for this error: {1}", path_to_load, all_others.ToString()));
            }

            if (!load_ok)
            {
                throw new Exception("Dll load of " + path_to_load + " failed.");
            }


            load_ok = false;
            _host = null;

            foreach (Type assembly_type in _dll.GetTypes())
            {
                if (assembly_type.BaseType == typeof(OldQuick.Inventory.OldQuickInventoryRepositoryHostable))
                {
                    _host = assembly_type;
                    break;
                }
                else
                {
                    Type[] all = assembly_type.GetInterfaces();
                }
            }


            if (_host == null)
            {
                throw new Exception("Dll load of " + path_to_load + "failed, could not find a hostable type.");
            }
        }
    }
}

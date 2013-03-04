using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InterfaceHost
{
    public class HostInterfaceMethodAttribute : Attribute
    {
        public string MethodName;
        public string ShortName;
        public HostInterfaceMethodAttribute() { this.MethodName = null; this.ShortName = null; }
        public HostInterfaceMethodAttribute(string MethodName) { this.MethodName = MethodName; this.ShortName = null; }
        public HostInterfaceMethodAttribute(string MethodName, string ShortName) { this.MethodName = MethodName; this.ShortName = ShortName; }
    }

}

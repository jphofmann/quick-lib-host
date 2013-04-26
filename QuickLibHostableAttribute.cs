using System;

namespace QuickHost
{
    public class QuickHostableAttribute : Attribute
    {
        public QuickHostableAttribute(string serviceName)
        {
            ServiceName = serviceName;
        }

        public string ServiceName { get; private set; }
    }
}

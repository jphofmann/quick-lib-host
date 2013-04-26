using System;

namespace QuickLibHostClient
{
    public class QuickLibHostableAttribute : Attribute
    {
        public QuickLibHostableAttribute(string serviceName)
        {
            ServiceName = serviceName;
        }

        public string ServiceName { get; private set; }
    }
}

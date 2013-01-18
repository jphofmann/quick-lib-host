using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.ServiceInterface;

namespace InterfaceHost
{
    public class QuickInventoryReflector : Service
    {
        public object Any(QuickHello request)
        {
            return new QuickResponse { Result = "Hello, " + request.Name };
        }

    }

    public class QuickResponse
    {
        public string Result { get; set; }
    }
    public class QuickHello
    {
        public string Name {get; set; }
    }

    public class ServiceStackHost : AppHostHttpListenerBase
    {
        public ServiceStackHost(string name) : base("Interface Host" + name, typeof(QuickInventoryReflector).Assembly) { }

        public override void Configure(Funq.Container container)
        {
            Routes.Add<QuickHello>("/hello").
                Add<QuickHello>("/hello/{Name}");
        }
    }
}

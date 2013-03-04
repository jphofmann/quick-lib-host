using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.ServiceInterface;
using ServiceStack.DataAnnotations;
using ServiceStack.Text;
using System.Runtime.Serialization;
using ServiceStack.ServiceModel.Serialization;
using ServiceStack.Logging.Support.Logging;

namespace QuickHost
{

    public class ServiceStackHost : AppHostHttpListenerBase
    {
        public ServiceStackHost(string name, Type[] to_host, Dictionary<string,Type> route_mapping) :
            base("Interface Host: " + name, to_host.ToList().Select( t => t.Assembly ).ToList().Distinct().ToArray() )
        {
            _routes = route_mapping;

        }

        private DebugLogFactory logger = null;
        private Dictionary<string, Type> _routes;
        public override void Configure(Funq.Container container)
        {
            logger = new DebugLogFactory();
            EndpointHostConfig endpoint_config = new EndpointHostConfig();
            endpoint_config.WsdlSoapActionNamespace = "http://api.quickhost.org/data";
            endpoint_config.WsdlServiceNamespace = "http://api.quickhost.org/data";
            endpoint_config.LogFactory = logger;
            //endpoint_config.
            SetConfig( endpoint_config );
            foreach (string route in _routes.Keys)
            {
                Routes.Add( _routes[route], "/" + route, "GET");
            }
        }
    }
}

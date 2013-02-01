using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OldQuick.Inventory;
using OldQuick.Inventory.Report;
using OldQuick.Inventory.Repository.Main;
using OldQuick.DB;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.ServiceInterface;
using ServiceStack.DataAnnotations;
using ServiceStack.Text;
using System.Runtime.Serialization;
using ServiceStack.ServiceModel.Serialization;
using ServiceStack.Logging.Support.Logging;

namespace InterfaceHost
{
    [DataContract(Namespace="http://api.quickhost.org/main")]
    public class QuickTestResponse
    {
        [DataMember]
        public string Result { get; set; }
    }

    [DataContract(Namespace="http://api.quickhost.org/main")]
    public class QuickTest
    {
        [DataMember]
        public string Name {get; set; }
    }

    public class QuickInventoryReflector : Service
    {
        public object Any(QuickTest request)
        {
            return new QuickTestResponse { Result = "Hello, " + request.Name };
        }
        public object Post(QuickTest request)
        {
            return new QuickTestResponse { Result = "Hello, posted " + request.Name };
        }

    }

    #region ItemIdentifier

    [DataContract(Namespace = "http://api.quickhost.org/main")]
    public class ItemIdentityLookup
    {
        [DataMember]
        public string OutsideIdentifier {get; set;}
    }

    [DataContract(Namespace = "http://api.quickhost.org/main")]
    public class ItemIdentityLookupResponse
    {
        [DataMember]
        public Tuple<ActionReport, IList<ItemIdentity>> Result { get; set; }
    }

    public class ItemIdentityLookupReflector : Service
    {
        public object Any(ItemIdentityLookup request)
        {
            return new ItemIdentityLookupResponse 
            { 
                Result = ServiceStackHost.Repo.ItemIdentityLookupByOutsideIdentifier(new List<string> {request.OutsideIdentifier})
            };
        }
    }

    #endregion

    public class ServiceStackHost : AppHostHttpListenerBase
    {
        public static IOldQuickInventoryRepository Repo = new OldQuickInstanceRepositoryRepository(DBNAMETAG.MAIN);

        public ServiceStackHost(string name) : base("Interface Host: " + name, typeof(QuickInventoryReflector).Assembly) { }

        private DebugLogFactory logger = null;
        public override void Configure(Funq.Container container)
        {
            logger = new DebugLogFactory();
            EndpointHostConfig endpoint_config = new EndpointHostConfig();
            endpoint_config.WsdlSoapActionNamespace = "http://api.quickhost.org/main";
            endpoint_config.WsdlServiceNamespace = "http://api.quickhost.org/main";
            endpoint_config.LogFactory = logger;
            //endpoint_config.
            SetConfig( endpoint_config );
            //new EndpointHostConfig { WsdlServiceNamespace = "http://api.quickhost.org/types" , WsdlSoapActionNamespace ="http://api.quickhost.org/soap" });
            
            

            // Add routes.
            Routes.Add<QuickTest>("/hello").Add<QuickTest>("/hello/{Name}");
            Routes.Add<ItemIdentityLookup>("/ItemIdentity/Lookup").Add<ItemIdentityLookup>("/ItemIdentity/Lookup/{OutsideIdentifier}");
        }
    }
}

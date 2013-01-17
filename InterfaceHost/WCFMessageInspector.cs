using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Collections.ObjectModel;

namespace InterfaceHost
{
    public class WCFMessageAttribute : Attribute, IServiceBehavior
    {
        public void ApplyDispatchBehavior(ServiceDescription desc, ServiceHostBase shb)
        {
            foreach( ChannelDispatcher cdisp in shb.ChannelDispatchers)
            {
                foreach( EndpointDispatcher edisp in cdisp.Endpoints )
                {
                    edisp.DispatchRuntime.MessageInspectors.Add( new WCFMessageInspector());
                }
            }
        }
        // use to check parameters.
        //public void ApplyDispatchBehavior(OperationDescription desc, DispatchOperation op)
       // {
        //    op.ParameterInspectors.Add(new WCFMessageInspector());
       // }
        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
    {
    }
        public void Validate(ServiceDescription desc, ServiceHostBase shb) { }

    }
    // from http://stackoverflow.com/questions/1653751/logging-requests-responses-in-a-wcf-rest-service
    class WCFMessageInspector : IDispatchMessageInspector
    {
        public WCFMessageInspector() { }


        public object AfterReceiveRequest( ref Message request, IClientChannel chan, InstanceContext ctx )
        {
            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
        }
    }
}

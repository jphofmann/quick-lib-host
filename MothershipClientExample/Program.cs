using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using MothershipClientExample.Mothership;

namespace MothershipClientExample
{
    class Program
    {
        static void Main(string[] args)
        {

            string Endpoint = "http://192.168.4.57/joinus/soap11";

            SyncReplyClient src = new SyncReplyClient(
                new BasicHttpBinding
                {
                    MaxReceivedMessageSize = int.MaxValue,
                    HostNameComparisonMode = HostNameComparisonMode.StrongWildcard
                },
                new EndpointAddress(Endpoint));

            OneWayClient owc = new OneWayClient(
                new BasicHttpBinding
                {
                    MaxReceivedMessageSize = int.MaxValue,
                    HostNameComparisonMode = HostNameComparisonMode.StrongWildcard
                },
                new EndpointAddress(Endpoint));


            owc.QuickHello("Victor Mancini");


        }
    }
}

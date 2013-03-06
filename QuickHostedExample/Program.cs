using System;
using QuickHost;

namespace QuickHostedExample
{
    public class ExampleHostable : OldQuickInventoryRepositoryHostable
    {
        private static readonly ExampleHost _exampleHost = new ExampleHost();

        public override object GetRepository()
        {
            throw new NotImplementedException();
        }

        public override string GetName()
        {
            return "ExampleHostable";
        }

        public override OldQuickInventoryHost GetHost()
        {
            return _exampleHost;
        }
    }

    public class ExampleHost : OldQuickInventoryHost
    {
        public void ExampeHost()
        {
            HostedShortName = "ExampleHost";
        }

        [HostInterfaceMethod("PokeTheExampleBear", "ExampleBearPoke")]
        public string PokeTheExampleBear(int os)
        {
            return String.Format("ExampleGR{0}AAR!", new String('O', os));
        }
    }
}

using System;
using QuickHost;

namespace QuickHostedExample
{
    [QuickHostable("ExampleHost")]
    public class ExampleHost
    {
        [QuickHostMethod("PokeTheExampleBear", "ExampleBearPoke")]
        public string PokeTheExampleBear(int os)
        {
            return String.Format("ExampleGR{0}AAR!", new String('O', os));
        }
    }
}

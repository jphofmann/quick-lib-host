using System;
using System.Runtime.Serialization;
using QuickLibHostClient;

namespace QuickLibHostedExample
{
    [QuickLibHostable("ExampleHost")]
    public class ExampleHost
    {
        [QuickLibHostMethod("StandardBearPoke")]
        public PokeTheExampleBearResult PokeTheExampleBear(int duration)
        {
            return PokeTheExampleBear(new PokeAttributes {Duration = duration, Strength = 5});
        }

        [QuickLibHostMethod("PokeTheExampleBear", "ExampleBearPoke")]
        public PokeTheExampleBearResult PokeTheExampleBear(PokeAttributes pokeAttributes)
        {
            return 
                new PokeTheExampleBearResult {
                    PokeInstanceNumber = 1,
                    Vocalization =
                        String.Format(
                            "ExampleGR{0}AAR!",
                            new String(pokeAttributes.Strength > 5 ? 'O' : 'o', pokeAttributes.Duration))};
        }

        [DataContract]
        public class PokeAttributes
        {
            [DataMember]
            public int Strength;

            [DataMember]
            public int Duration;
        }

        [DataContract]
        public class PokeTheExampleBearResult
        {
            [DataMember]
            public int PokeInstanceNumber;
            
            [DataMember]
            public String Vocalization;
        }
    }
}

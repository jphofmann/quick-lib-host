using System;
using System.Runtime.Serialization;
using QuickHost;
using OldQuick;
using OldQuick.DB;
using OldQuick.ItemIdentification.Book;

namespace QuickHostedExample
{
    [QuickHostable("ExampleHost")]
    public class ExampleHost
    {

        [QuickHostMethod("StandardBearPoke")]
        public PokeTheExampleBearResult PokeTheExampleBear(int duration)
        {
            return PokeTheExampleBear(new PokeAttributes {Duration = duration, Strength = 5});
        }

        [QuickHostMethod("PokeTheExampleBear", "ExampleBearPoke")]
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

        [QuickHostMethod("LookupAddress")]   
        public clsAddress LookupAddress(int id)
        {
            return clsAddress.LoadFromCache(id);
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

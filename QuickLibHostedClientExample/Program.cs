/* 
 * Copyright (C) 2013 the QuickLibHost contributors. All rights reserved.
 * 
 * This file is part of QuickLibHost.
 * 
 * QuickLibHost is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * QuickLibHost is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.

 * You should have received a copy of the GNU Lesser General Public License
 * along with QuickLibHost.  If not, see <http://www.gnu.org/licenses/>.
 */

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

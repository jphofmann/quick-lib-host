using System.Collections.Generic;

namespace QuickHost
{
    public class QuickHostableClassMappings
    {
        private static readonly Dictionary<string, object> Map = new Dictionary<string, object>();

        public static void AddHostedClass(string key, object quickHostableClass)
        {
            Map.Add(key, quickHostableClass);
        }

        public static object GetHostedClass(string key)
        {
            return Map[key];
        }
    }
}

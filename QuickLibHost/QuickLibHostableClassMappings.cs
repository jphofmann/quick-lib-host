using System.Collections.Generic;

namespace QuickLibHost
{
    public class QuickLibHostableClassMappings
    {
        private static readonly Dictionary<string, object> Map = new Dictionary<string, object>();

        public static void AddHostedClass(string key, object quickLibHostableClass)
        {
            Map.Add(key, quickLibHostableClass);
        }

        public static object GetHostedClass(string key)
        {
            return Map[key];
        }
    }
}

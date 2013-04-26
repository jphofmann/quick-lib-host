using System;

namespace QuickLibHost
{
    public class QuickLibHostMethodAttribute : Attribute
    {
        public QuickLibHostMethodAttribute(string methodAlias, string restUriAlias)
        {
            MethodAlias = methodAlias;
            RestUriAlias = restUriAlias;
        }

        public QuickLibHostMethodAttribute(string methodAlias)
            : this(methodAlias, methodAlias)
        { }

        public string MethodAlias { get; private set; }
        public string RestUriAlias { get; private set; }
    }
}

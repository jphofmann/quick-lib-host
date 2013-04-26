using System;

namespace QuickHost
{
    public class QuickHostMethodAttribute : Attribute
    {
        public QuickHostMethodAttribute(string methodAlias, string restUriAlias)
        {
            MethodAlias = methodAlias;
            RestUriAlias = restUriAlias;
        }

        public QuickHostMethodAttribute(string methodAlias)
            : this(methodAlias, methodAlias)
        { }

        public string MethodAlias { get; private set; }
        public string RestUriAlias { get; private set; }
    }
}

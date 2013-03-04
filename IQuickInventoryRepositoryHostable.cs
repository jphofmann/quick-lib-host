using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace QuickHost
{
    public class OldQuickInventoryRepositoryHostable
    {
        public OldQuickInventoryRepositoryHostable() {}
        public virtual object GetRepository() { return null; }
        public virtual string GetName() { return "Invalid Hostable Repository"; }
        public virtual OldQuickInventoryHost GetHost() { return null; }
    }

    public class OldQuickInventoryHost
    {
        public virtual String HostedShortName { get { return "BaseHost"; } set { } }
    }
}

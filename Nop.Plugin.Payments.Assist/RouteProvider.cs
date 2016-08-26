using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.Assist
{
    public partial class RouteProvider : IRouteProvider
    {
        #region Methods

        public void RegisterRoutes(RouteCollection routes)
        {
            //return
            routes.MapRoute("Plugin.Payments.Assist.Return",
                 "Plugins/PaymentAssist/Return",
                 new { controller = "PaymentAssist", action = "Return" },
                 new[] { "Nop.Plugin.Payments.Assist.Controllers" }
            );
            //fail
            routes.MapRoute("Plugin.Payments.Assist.Fail",
                 "Plugins/PaymentAssist/Fail",
                 new { controller = "PaymentAssist", action = "Fail" },
                 new[] { "Nop.Plugin.Payments.Assist.Controllers" }
            );
        }

        #endregion

        #region Properties

        public int Priority
        {
            get
            {
                return 0;
            }
        }

        #endregion
    }
}

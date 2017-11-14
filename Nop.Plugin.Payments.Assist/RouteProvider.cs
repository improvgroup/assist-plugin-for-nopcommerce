using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Assist
{
    public partial class RouteProvider : IRouteProvider
    {
        #region Methods

        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //return
            routeBuilder.MapRoute("Plugin.Payments.Assist.Return",
                "Plugins/PaymentAssist/Return",
                new {controller = "PaymentAssist", action = "Return"});
            //fail
            routeBuilder.MapRoute("Plugin.Payments.Assist.Fail",
                "Plugins/PaymentAssist/Fail",
                new {controller = "PaymentAssist", action = "Fail"});
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

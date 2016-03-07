using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Assist.Models;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Assist.Controllers
{
    public class PaymentAssistController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IWebHelper _webHelper;
        private readonly AssistPaymentSettings _assistPaymentSettings;
        private readonly PaymentSettings _paymentSettings;
        private readonly IWorkContext _workContext;

        public PaymentAssistController(ISettingService settingService, 
            IPaymentService paymentService, IOrderService orderService, 
            IOrderProcessingService orderProcessingService, IWebHelper webHelper,
            AssistPaymentSettings assistPaymentSettings,
            PaymentSettings paymentSettings, IWorkContext workContext)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._webHelper = webHelper;
            this._assistPaymentSettings = assistPaymentSettings;
            this._paymentSettings = paymentSettings;
            this._workContext = workContext;
        }
        
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new ConfigurationModel();
            model.GatewayUrl = _assistPaymentSettings.GatewayUrl;
            model.ShopId = _assistPaymentSettings.ShopId;
            model.AuthorizeOnly = _assistPaymentSettings.AuthorizeOnly;
            model.TestMode = _assistPaymentSettings.TestMode;
            model.AdditionalFee = _assistPaymentSettings.AdditionalFee;

            return View("~/Plugins/Payments.Assist/Views/PaymentAssist/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //save settings
            _assistPaymentSettings.GatewayUrl = model.GatewayUrl;
            _assistPaymentSettings.ShopId = model.ShopId;
            _assistPaymentSettings.AuthorizeOnly = model.AuthorizeOnly;
            _assistPaymentSettings.TestMode = model.TestMode;
            _assistPaymentSettings.AdditionalFee = model.AdditionalFee;
            _settingService.SaveSetting(_assistPaymentSettings);
            
            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();
            return View("~/Plugins/Payments.Assist/Views/PaymentAssist/PaymentInfo.cshtml", model);
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [ValidateInput(false)]
        public ActionResult Return(FormCollection form)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Assist") as AssistPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("Assist module cannot be loaded");

            //UNDONE: we should ensure this page is submitted by Assist; otherwise, everybody can mark his own order as paid
            //uncomment the libe below when it's ready
            return RedirectToAction("Index", "Home", new { area = "" });


            var order = _orderService.GetOrderById(_webHelper.QueryString<int>("Order_IDP"));
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            if (_assistPaymentSettings.AuthorizeOnly)
            {
                if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                {
                    _orderProcessingService.MarkAsAuthorized(order);
                }
            }
            else
            {
                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    _orderProcessingService.MarkOrderAsPaid(order);
                }
            }

            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }
    }
}
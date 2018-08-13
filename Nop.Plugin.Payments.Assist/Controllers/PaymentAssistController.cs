using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.Assist.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Assist.Controllers
{
    public class PaymentAssistController : BasePaymentController
    {
        #region Fields

        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ILocalizationService _localizationService;
        private readonly AssistPaymentSettings _assistPaymentSettings;
        private readonly IPermissionService _permissionService;

        #endregion

        #region Ctor

        public PaymentAssistController(ISettingService settingService, 
            IPaymentService paymentService, 
            IOrderService orderService, 
            IOrderProcessingService orderProcessingService, 
            IWebHelper webHelper, 
            IWorkContext workContext, 
            ILocalizationService localizationService,
            AssistPaymentSettings assistPaymentSettings,
            IPermissionService permissionService)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._webHelper = webHelper;
            this._workContext = workContext;
            this._localizationService = localizationService;
            this._assistPaymentSettings = assistPaymentSettings;
            this._permissionService = permissionService;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                MerchantId = _assistPaymentSettings.MerchantId,
                GatewayUrl = _assistPaymentSettings.GatewayUrl,
                AuthorizeOnly = _assistPaymentSettings.AuthorizeOnly,
                TestMode = _assistPaymentSettings.TestMode,
                AdditionalFee = _assistPaymentSettings.AdditionalFee,
                Login = _assistPaymentSettings.Login,
                Password = _assistPaymentSettings.Password
            };

            return View("~/Plugins/Payments.Assist/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //save settings
            _assistPaymentSettings.GatewayUrl = model.GatewayUrl;
            _assistPaymentSettings.MerchantId = model.MerchantId;
            _assistPaymentSettings.AuthorizeOnly = model.AuthorizeOnly;
            _assistPaymentSettings.TestMode = model.TestMode;
            _assistPaymentSettings.AdditionalFee = model.AdditionalFee;
            _assistPaymentSettings.Login = model.Login;
            _assistPaymentSettings.Password = model.Password;

            _settingService.SaveSetting(_assistPaymentSettings);

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }
        
        public IActionResult Fail()
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Assist") as AssistPaymentProcessor;

            if (processor == null
                || !_paymentService.IsPaymentMethodActive(processor)
                || !processor.PluginDescriptor.Installed)
                throw new NopException("Assist module cannot be loaded");

            var order = _orderService.GetOrderById(_webHelper.QueryString<int>("ordernumber"));
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return RedirectToRoute("HomePage");

            return RedirectToRoute("OrderDetails", new { orderId = order.Id });
        }

        public IActionResult Return()
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Assist") as AssistPaymentProcessor;

            if (processor == null 
                || !_paymentService.IsPaymentMethodActive(processor) 
                || !processor.PluginDescriptor.Installed)
                throw new NopException("Assist module cannot be loaded");

            var order = _orderService.GetOrderById(_webHelper.QueryString<int>("ordernumber"));
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return RedirectToRoute("HomePage");

            if (!processor.CheckPaymentStatus(order))
                return RedirectToRoute("HomePage");

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

        #endregion
    }
}
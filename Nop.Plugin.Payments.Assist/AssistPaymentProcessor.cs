using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web.Routing;
using System.Xml.Linq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Assist.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Web.Framework;

namespace Nop.Plugin.Payments.Assist
{
    /// <summary>
    /// Assist payment processor
    /// </summary>
    public class AssistPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly ICurrencyService _currencyService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly CurrencySettings _currencySettings;
        private readonly AssistPaymentSettings _assistPaymentSettings;
        private readonly ILocalizationService _localizationService;

        private const string TestAssistPaymentUrl = "https://test.paysecure.ru/";
        private const string PaymentCommand = "pay/order.cfm";
        private const string OrderstateCommend = "orderstate/orderstate.cfm";

        #endregion

        #region Ctor

        public AssistPaymentProcessor(ICurrencyService currencyService,
            ISettingService settingService, 
            IWebHelper webHelper, 
            AssistPaymentSettings assistPaymentSettings, 
            CurrencySettings currencySettings,
            ILocalizationService localizationService)
        {
            this._currencyService = currencyService;
            this._settingService = settingService;
            this._webHelper = webHelper;
            this._assistPaymentSettings = assistPaymentSettings;
            this._currencySettings = currencySettings;
            this._localizationService = localizationService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Check payment status
        /// </summary>
        /// <param name="order">Order for check payment status</param>
        /// <returns>True if payment status is Approved, Felse - Otherwise</returns>
        public bool CheckPaymentStatus(Order order)
        {
            var searchFrom = order.CreatedOnUtc;

            //create and send post data
            var postData = new NameValueCollection
            {
                { "Merchant_ID", _assistPaymentSettings.MerchantId },
                { "Login", _assistPaymentSettings.Login },
                { "Password", _assistPaymentSettings.Password },
                { "OrderNumber", order.Id.ToString() },
                { "StartYear", searchFrom.Year.ToString() },
                { "StartMonth", searchFrom.Month.ToString() },
                { "StartDay", searchFrom.Day.ToString() },
                { "StartHour", "0" },
                { "StartMin", "0" },
                // response on XML format 
                { "Format", "3" }
            };

            byte[] data;
            using (var client = new WebClient())
            {
                data = client.UploadValues(GetUrl(OrderstateCommend), postData);
            }

            using (var ms = new MemoryStream(data))
            {
                using (var sr = new StreamReader(ms))
                {
                    var rez = sr.ReadToEnd();

                    if (!rez.Contains("?xml"))
                        return false;

                    try
                    {
                        var doc = XDocument.Parse(rez);
                        var orderElement = doc.Root.Return(e => e.Element("order"), new XElement("order"));

                        var flag = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", order.OrderTotal) == orderElement.Return(e => e.Element("orderamount"), new XElement("orderamount", "0.00")).Value;
                        flag = flag && orderElement.Return(e => e.Element("orderstate"), new XElement("orderstate")).Value == "Approved";

                        return flag;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
        }

        public string GetUrl(string command)
        {
            var server = (_assistPaymentSettings.TestMode ? TestAssistPaymentUrl : _assistPaymentSettings.GatewayUrl).TrimEnd('/');

            return string.Format("{0}/{1}", server, command);
        }

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult { NewPaymentStatus = PaymentStatus.Pending };

            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var post = new RemotePost
            {
                FormName = "AssistPaymentForm",
                Url = GetUrl(PaymentCommand),
                Method = "POST"
            };

            post.Add("Merchant_ID", _assistPaymentSettings.MerchantId);
            post.Add("Delay", _assistPaymentSettings.AuthorizeOnly ? "1" : "0");
            post.Add("OrderNumber", postProcessPaymentRequest.Order.Id.ToString());
            post.Add("OrderAmount", string.Format(CultureInfo.InvariantCulture, "{0:0.00}", postProcessPaymentRequest.Order.OrderTotal));
            post.Add("OrderCurrency", _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode);
            post.Add("URL_RETURN", string.Format("{0}Plugins/PaymentAssist/Fail", _webHelper.GetStoreLocation()));
            post.Add("URL_RETURN_OK", string.Format("{0}Plugins/PaymentAssist/Return", _webHelper.GetStoreLocation()));
            post.Add("FirstName", postProcessPaymentRequest.Order.BillingAddress.FirstName);
            post.Add("LastName", postProcessPaymentRequest.Order.BillingAddress.LastName);
            post.Add("Email", postProcessPaymentRequest.Order.BillingAddress.Email);
            post.Add("Address", postProcessPaymentRequest.Order.BillingAddress.Address1);
            post.Add("City", postProcessPaymentRequest.Order.BillingAddress.City);
            post.Add("Zip", postProcessPaymentRequest.Order.BillingAddress.ZipPostalCode);
            post.Add("Phone", postProcessPaymentRequest.Order.BillingAddress.PhoneNumber);

            var state = postProcessPaymentRequest.Order.BillingAddress.StateProvince;

            if (state != null)
            {
                post.Add("State", state.Abbreviation);
            }

            var country = postProcessPaymentRequest.Order.BillingAddress.Country;

            if (country != null)
            {
                post.Add("Country", country.ThreeLetterIsoCode);
            }

            post.Post();
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return _assistPaymentSettings.AdditionalFee;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();

            result.AddError("Capture method not supported");

            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();

            result.AddError("Refund method not supported");

            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();

            result.AddError("Void method not supported");

            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            result.AddError("Recurring payment not supported");

            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();

            result.AddError("Recurring payment not supported");

            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //Assist is the redirection payment method
            //It also validates whether order is also paid (after redirection) so customers will not be able to pay twice
            
            //payment status should be Pending
            if (order.PaymentStatus != PaymentStatus.Pending)
                return false;

            //let's ensure that at least 1 minute passed after order is placed
            return !((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1);
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentAssist";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Assist.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentAssist";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Assist.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentAssistController);
        }

        public override void Install()
        {
            var settings = new AssistPaymentSettings
            {
                GatewayUrl = TestAssistPaymentUrl,
                MerchantId = "",
                AuthorizeOnly = false,
                TestMode = true,
                AdditionalFee = 0
            };

            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.RedirectionTip", "You will be redirected to Assist site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.GatewayUrl", "Gateway URL");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.GatewayUrl.Hint", "Enter gateway URL.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.MerchantId", "Merchant ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.MerchantId.Hint", "Enter your merchant identifier.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.AuthorizeOnly", "Authorize only");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.AuthorizeOnly.Hint", "Authorize only?");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.TestMode", "Test mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.TestMode.Hint", "Is test mode?");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.PaymentMethodDescription", "You will be redirected to Assist site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.Password", "Password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.Password.Hint", "Set the password.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.Login", "Login");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Assist.Login.Hint", "Set the login.");

            base.Install();
        }

        public override void Uninstall()
        {
            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.GatewayUrl");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.GatewayUrl.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.MerchantId");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.MerchantId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.AuthorizeOnly");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.AuthorizeOnly.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.TestMode");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.TestMode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.PaymentMethodDescription");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.Password");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.Password.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.Login");
            this.DeletePluginLocaleResource("Plugins.Payments.Assist.Login.Hint");

            base.Uninstall();
        }
        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            get { return _localizationService.GetResource("Plugins.Payments.Assist.PaymentMethodDescription"); }
        }

        #endregion
    }
}

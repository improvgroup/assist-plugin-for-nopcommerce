using System.ComponentModel;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Assist.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payments.Assist.GatewayUrl")]
        public string GatewayUrl { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Assist.MerchantId")]
        public string MerchantId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Assist.AuthorizeOnly")]
        public bool AuthorizeOnly { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Assist.TestMode")]
        public bool TestMode { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Assist.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
    }
}
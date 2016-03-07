using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Assist
{
    public class AssistPaymentSettings : ISettings
    {
        public string GatewayUrl { get; set; }
        public string ShopId { get; set; }
        public bool AuthorizeOnly { get; set; }
        public bool TestMode { get; set; }
        public decimal AdditionalFee { get; set; }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Web.Routing;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.MonetaAssist.Controllers;
using Nop.Plugin.Payments.MonetaAssist.Models;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework;

namespace Nop.Plugin.Payments.MonetaAssist
{
    /// <summary>
    /// Moneta.Assistent payment method
    /// </summary>
    public class MonetaAssistPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly MonetaAssistPaymentSettings _monetaAssistPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        #endregion

        #region Ctor

        public MonetaAssistPaymentProcessor(MonetaAssistPaymentSettings monetaAssistPaymentSettings,
            ISettingService settingService,
            ICurrencyService currencyService,
            CurrencySettings currencySettings,
            IOrderTotalCalculationService orderTotalCalculationService)
        {
            this._monetaAssistPaymentSettings = monetaAssistPaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._orderTotalCalculationService = orderTotalCalculationService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult {NewPaymentStatus = PaymentStatus.Pending};
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var customerId = postProcessPaymentRequest.Order.CustomerId;
            var orderGuid = postProcessPaymentRequest.Order.OrderGuid;
            var orderTotal = postProcessPaymentRequest.Order.OrderTotal;

            var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode;

            var model = PaymentInfoModel.CreatePaymentInfoModel(_monetaAssistPaymentSettings, customerId, orderGuid, orderTotal, currencyCode);
           
            //Make and send post data
            var post = new RemotePost
            {
                FormName = "PayPoint",
                Url = model.MonetaAssistantUrl
            };
            post.Add("MNT_ID", model.MntId);
            post.Add("MNT_TRANSACTION_ID", model.MntTransactionId);
            post.Add("MNT_CURRENCY_CODE", model.MntCurrencyCode);
            post.Add("MNT_AMOUNT", model.MntAmount);
            post.Add("MNT_TEST_MODE", model.MntTestMode.ToString());
            post.Add("MNT_SUBSCRIBER_ID", model.MntSubscriberId.ToString());
            post.Add("MNT_SIGNATURE", model.MntSignature);
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
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _monetaAssistPaymentSettings.AdditionalFee, _monetaAssistPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            return !((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5);
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
            controllerName = "PaymentMonetaAssist";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.MonetaAssist.Controllers" }, { "area", null } };
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
            controllerName = "PaymentMonetaAssist";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.MonetaAssist.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Get controller type
        /// </summary>
        /// <returns>Controller type</returns>
        public Type GetControllerType()
        {
            return typeof(PaymentMonetaAssistController);
        }
 
        /// <summary>
        /// Install plugin method
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new MonetaAssistPaymentSettings
            {
                MntTestMode = true,
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.MntId", "Store identifier");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.MntId.Hint", "Specify the account ID of your store on the website moneta.ru (MNT_ID)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.MntTestMode", "Test mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.MntTestMode.Hint", "Check to enable test mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.Hashcode", "Hashcode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.Hashcode.Hint", "Set the data integrity code");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.RedirectionTip",
                "For payment you will be redirected to the website MONETA.RU");

            base.Install();
        }

        /// <summary>
        /// Uninstall plugin method
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<MonetaAssistPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.MntId");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.MntTestMode");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.Hashcode");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.MntId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.MntTestMode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.Hashcode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.MonetaAssist.Fields.RedirectionTip");

            base.Uninstall();
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
        #endregion

        #region Properties
        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        #endregion
    }
}
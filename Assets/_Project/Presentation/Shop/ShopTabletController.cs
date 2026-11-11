using System;
using VrGame.Data.Shop;
using VrGame.Domain.Shop;
using VrGame.Domain.Economy;
using UnityEngine;

namespace VrGame.Presentation.Shop
{
    public interface IEconomyChangeSource
    {
        event Action<BalanceChanged> Changed;
    }

    public interface IShopTabletView
    {
        void Render(ShopCatalog catalog, int selectedIndex, int balance, bool open, string feedback, bool canInteract);
    }

    public sealed class ShopTabletController : MonoBehaviour
    {
        private ShopCatalog catalog;
        private EconomyService economy;
        private PurchaseService purchases;
        private IEconomyChangeSource changes;
        private IShopTabletView view;
        private bool subscribed, submitting, paused, focused = true;
        private bool trackingAvailable = true;
        private int lastFrame = -1;
        private ProductId lastProduct;
        private PurchaseResult lastResult;
        private string feedback = string.Empty;
        public bool IsOpen { get; private set; }
        public int SelectedIndex { get; private set; }
        public bool CanInteract => isActiveAndEnabled && focused && trackingAvailable && !paused && catalog != null && !submitting;

        public void Configure(ShopCatalog shop, EconomyService sessionEconomy, PurchaseService service, IEconomyChangeSource source, IShopTabletView tabletView)
        {
            Unsubscribe();
            catalog = shop ?? throw new ArgumentNullException(nameof(shop));
            economy = sessionEconomy ?? throw new ArgumentNullException(nameof(sessionEconomy));
            purchases = service ?? throw new ArgumentNullException(nameof(service));
            changes = source ?? throw new ArgumentNullException(nameof(source));
            view = tabletView ?? throw new ArgumentNullException(nameof(tabletView));
            IsOpen = false;
            Subscribe();
            Refresh();
        }

        public void Toggle()
        {
            if (CanInteract)
            {
                IsOpen = !IsOpen;
                Refresh();
            }
        }

        public void Close()
        {
            IsOpen = false;
            Refresh();
        }

        public void Next() => Select(1);
        public void Previous() => Select(-1);
        private void Select(int offset)
        {
            if (!CanInteract || !IsOpen || catalog.Definitions.Count == 0)
                return;
            SelectedIndex = (SelectedIndex + offset + catalog.Definitions.Count) % catalog.Definitions.Count;
            feedback = string.Empty;
            Refresh();
        }

        public void RequestPurchase() => Confirm();
        public PurchaseResult Confirm()
        {
            if (!CanInteract || !IsOpen || catalog.Definitions.Count == 0)
                return null;
            var offer = catalog.Definitions[SelectedIndex];
            if (lastFrame == Time.frameCount && lastProduct.Equals(offer.Id))
                return lastResult;
            lastFrame = Time.frameCount;
            lastProduct = offer.Id;
            submitting = true;
            try
            {
                lastResult = purchases.Purchase(
                    offer.Id,
                    new EconomyChangeContext(
                        "Подтверждение покупки на планшете магазина",
                        Guid.NewGuid(),
                        Guid.NewGuid()));
                feedback = lastResult.Succeeded ? "Покупка оформлена" : lastResult.EconomyRejection == EconomyRejection.InsufficientFunds ? "Недостаточно средств" : "Покупка отклонена";
                return lastResult;
            }
            finally
            {
                submitting = false;
                Refresh();
            }
        }

        public void SetTrackingAvailable(bool value)
        {
            trackingAvailable = value;
            if (!value)
                IsOpen = false;
            Refresh();
        }

        public void SetApplicationPaused(bool value)
        {
            paused = value;
            if (value)
                IsOpen = false;
            Refresh();
        }

        public void SetApplicationFocused(bool value)
        {
            focused = value;
            if (!value)
                IsOpen = false;
            Refresh();
        }

        private void OnApplicationPause(bool value) => SetApplicationPaused(value);
        private void OnApplicationFocus(bool value) => SetApplicationFocused(value);
        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            IsOpen = false;
            Refresh();
        }

        private void Subscribe()
        {
            if (!subscribed && changes != null && isActiveAndEnabled)
            {
                changes.Changed += OnBalance;
                subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (subscribed)
            {
                changes.Changed -= OnBalance;
                subscribed = false;
            }
        }

        private void OnBalance(BalanceChanged _) => Refresh();
        private void Refresh()
        {
            if (view != null)
                view.Render(catalog, SelectedIndex, economy.Balance, IsOpen, feedback, CanInteract);
        }
    }
}

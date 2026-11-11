using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VrGame.Data.Shop;
using VrGame.Domain.Economy;
using VrGame.Domain.Shop;
using VrGame.Presentation.Shop;

namespace VrGame.Bootstrap
{
    public sealed class ShopTabletFactory : MonoBehaviour, IShopTabletView
    {
        private GameObject panel;
        private Text content;
        private Button tab, previous, next, confirm, close;
        private Canvas tabletCanvas;
        private ShopTabletController controller;
        public static ShopTabletController Create(Transform anchor, ShopCatalog catalog, EconomyService economy, PurchaseService purchases, EconomyEventRelay events)
        {
            if (anchor == null)
                throw new ArgumentNullException(nameof(anchor));
            var viewer = Camera.main;
            if (viewer == null)
                throw new InvalidOperationException("Основная XR camera для планшета магазина не найдена.");

            var host = new GameObject(
                "Планшет магазина",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(TrackedDeviceGraphicRaycaster));
            host.transform.SetParent(anchor, false);
            host.transform.localPosition = new Vector3(0, .12f, .15f);
            host.transform.localRotation = Quaternion.Euler(65, 0, 0);
            host.transform.localScale = Vector3.one * .0006f;
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 650);
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = viewer;
            var factory = host.AddComponent<ShopTabletFactory>();
            factory.tabletCanvas = canvas;
            factory.Build();
            factory.controller = host.AddComponent<ShopTabletController>();
            factory.controller.Configure(catalog, economy, purchases, events, factory);
            factory.controller.SetTrackingAvailable(false);
            canvas.enabled = false;
            factory.Bind();
            return factory.controller;
        }

        private void Build()
        {
            tab = Button(transform, "Магазин", new Vector2(0, -290));
            panel = new GameObject("Каталог", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 540);
            panel.GetComponent<Image>().color = new Color(.04f, .07f, .1f, .98f);
            content = Label(panel.transform, "", new Vector2(0, 65), new Vector2(560, 370));
            previous = Button(panel.transform, "Назад", new Vector2(-185, -170));
            next = Button(panel.transform, "Далее", new Vector2(0, -170));
            confirm = Button(panel.transform, "Купить", new Vector2(185, -170));
            close = Button(panel.transform, "Закрыть", new Vector2(0, -235));
        }

        private static Text Label(Transform parent, string value, Vector2 position, Vector2 size)
        {
            var host = new GameObject("Текст", typeof(RectTransform), typeof(Text));
            host.transform.SetParent(parent, false);
            var rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = host.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(Transform parent, string label, Vector2 position)
        {
            var host = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            host.transform.SetParent(parent, false);
            var rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170, 50);
            rect.anchoredPosition = position;
            host.GetComponent<Image>().color = new Color(.15f, .3f, .45f);
            var text = Label(host.transform, label, Vector2.zero, new Vector2(165, 45));
            text.alignment = TextAnchor.MiddleCenter;
            return host.GetComponent<Button>();
        }

        private bool tracked;
        private void LateUpdate()
        {
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
            var available = device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var value) && value;
            tabletCanvas.enabled = available;
            if (tracked != available)
            {
                tracked = available;
                controller.SetTrackingAvailable(available);
            }
        }

        private void OnEnable()
        {
            if (controller != null)
                Bind();
        }

        private void OnDisable()
        {
            if (controller != null)
                Unbind();
        }

        private void Bind()
        {
            Unbind();
            tab.onClick.AddListener(controller.Toggle);
            previous.onClick.AddListener(controller.Previous);
            next.onClick.AddListener(controller.Next);
            confirm.onClick.AddListener(controller.RequestPurchase);
            close.onClick.AddListener(controller.Close);
        }

        private void Unbind()
        {
            tab.onClick.RemoveListener(controller.Toggle);
            previous.onClick.RemoveListener(controller.Previous);
            next.onClick.RemoveListener(controller.Next);
            confirm.onClick.RemoveListener(controller.RequestPurchase);
            close.onClick.RemoveListener(controller.Close);
        }

        public void Render(ShopCatalog catalog, int selectedIndex, int balance, bool open, string feedback, bool canInteract)
        {
            panel.SetActive(open);
            tab.interactable = canInteract;
            var text = new StringBuilder("Баланс: " + balance + "\n");
            for (var i = 0; i < catalog.Definitions.Count; i++)
            {
                var offer = catalog.Definitions[i];
                text.Append(i == selectedIndex ? "▶ " : "  ").Append(offer.DisplayName).Append(" ×").Append(offer.Quantity).Append(" — ").Append(offer.Price).AppendLine();
            }

            text.AppendLine().Append(feedback);
            content.text = text.ToString();
            previous.interactable = next.interactable = confirm.interactable = close.interactable = canInteract;
        }
    }
}

using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VrGame.Domain.Orders;

namespace VrGame.Presentation.Orders
{
    public sealed class OrderBoardTextView : MonoBehaviour, IOrderBoardView
    {
        [SerializeField] private Text ordersText;
        [SerializeField] private Text selectionText;
        [SerializeField] private Text activeOrderText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button acceptButton;

        private static readonly Color SuccessColor = new Color(.35f, 1f, .45f);
        private static readonly Color WarningColor = new Color(1f, .78f, .2f);
        private static readonly Color FailureColor = new Color(1f, .32f, .28f);

        public void Configure(Text orders, Text selection, Text active, Text status, Button accept)
        {
            ordersText = orders ?? throw new ArgumentNullException(nameof(orders));
            selectionText = selection ?? throw new ArgumentNullException(nameof(selection));
            activeOrderText = active ?? throw new ArgumentNullException(nameof(active));
            statusText = status ?? throw new ArgumentNullException(nameof(status));
            acceptButton = accept ?? throw new ArgumentNullException(nameof(accept));
        }

        public void Render(OrderBoardSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            EnsureConfigured();

            var list = new StringBuilder();
            for (var index = 0; index < snapshot.Orders.Count; index++)
            {
                if (index > 0)
                    list.AppendLine();
                var order = snapshot.Orders[index];
                list.Append(index == snapshot.SelectedIndex ? "▶ " : "  ");
                list.Append(order.DisplayName);
                list.Append(" [");
                list.Append(StateLabel(order.State));
                list.Append(']');
            }
            ordersText.text = list.ToString();

            var selected = snapshot.SelectedOrder;
            selectionText.text = selected == null
                ? "Доступных заказов нет"
                : $"{selected.DisplayName}\n{selected.MedicineDisplayName} ×{selected.Quantity}\nНаграда: {selected.Reward}";

            var active = snapshot.ActiveOrder;
            activeOrderText.text = active == null
                ? "Активный заказ: нет"
                : $"АКТИВНЫЙ ЗАКАЗ\n{active.MedicineDisplayName} ×{active.Quantity}\nНаграда: {active.Reward}";
            activeOrderText.color = active == null ? Color.white : SuccessColor;
            acceptButton.interactable = snapshot.CanAcceptSelected;
            RenderStatus(snapshot);
        }

        private void RenderStatus(OrderBoardSnapshot snapshot)
        {
            statusText.color = Color.white;
            switch (snapshot.Feedback)
            {
                case OrderBoardFeedback.None:
                    if (snapshot.ActiveOrder != null)
                    {
                        statusText.text = "Сначала завершите активный заказ";
                        statusText.color = WarningColor;
                    }
                    else if (snapshot.SelectedOrder == null)
                    {
                        statusText.text = "Заказы отсутствуют";
                    }
                    else if (!snapshot.SelectedOrder.IsAvailable)
                    {
                        statusText.text = "Этот заказ недоступен";
                        statusText.color = WarningColor;
                    }
                    else
                    {
                        statusText.text = "Заказ можно принять";
                    }
                    return;
                case OrderBoardFeedback.Accepted:
                    statusText.text = "Заказ принят. Следуйте к точке доставки";
                    statusText.color = SuccessColor;
                    return;
                case OrderBoardFeedback.Replay:
                    statusText.text = "Этот запрос уже обработан";
                    statusText.color = SuccessColor;
                    return;
                case OrderBoardFeedback.ActiveOrderExists:
                    statusText.text = "Нельзя принять второй заказ: уже есть активный";
                    statusText.color = WarningColor;
                    return;
                case OrderBoardFeedback.Unavailable:
                    statusText.text = "Выбранный заказ больше недоступен";
                    statusText.color = WarningColor;
                    return;
                default:
                    statusText.text = FormatFailure(snapshot.LastResult);
                    statusText.color = FailureColor;
                    return;
            }
        }

        private static string StateLabel(OrderState state)
        {
            switch (state)
            {
                case OrderState.Available: return "доступен";
                case OrderState.InProgress: return "активен";
                case OrderState.Completed: return "завершён";
                default: return state.ToString();
            }
        }

        private static string FormatFailure(OrderMutationResult result)
        {
            if (result == null)
                return "Принятие заказа отклонено";
            switch (result.Rejection)
            {
                case OrderRejection.ActiveOrderExists:
                    return "Уже есть активный заказ";
                case OrderRejection.UnknownOrder:
                    return "Заказ больше не существует";
                case OrderRejection.InvalidTransition:
                case OrderRejection.AlreadyCompleted:
                    return "Заказ больше нельзя принять";
                case OrderRejection.CommandConflict:
                    return "Конфликт повторной команды";
                default:
                    return "Принятие заказа отклонено";
            }
        }

        private void EnsureConfigured()
        {
            if (ordersText == null || selectionText == null || activeOrderText == null ||
                statusText == null || acceptButton == null)
                throw new InvalidOperationException("Текстовое представление Order Board не настроено.");
        }
    }
}

using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VrGame.Domain.SeedStorage;

namespace VrGame.Presentation.SeedStorage
{
    public sealed class SeedStorageStationTextView : MonoBehaviour, ISeedStorageStationView
    {
        [Serializable]
        private struct PlantLabel
        {
            [SerializeField]
            private string plantTypeId;

            [SerializeField]
            private string label;

            public string PlantTypeId => plantTypeId;
            public string Label => label;
        }

        [SerializeField]
        private Text stockText;

        [SerializeField]
        private Text selectionText;

        [SerializeField]
        private Text statusText;

        [SerializeField]
        private PlantLabel[] plantLabels = Array.Empty<PlantLabel>();

        public void Configure(Text stock, Text selection, Text status)
        {
            stockText = stock ?? throw new ArgumentNullException(nameof(stock));
            selectionText = selection ?? throw new ArgumentNullException(nameof(selection));
            statusText = status ?? throw new ArgumentNullException(nameof(status));
        }

        public void Render(
            SeedStorageSnapshot snapshot,
            int selectedIndex,
            int requestedQuantity,
            SeedDispenseResult lastResult)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            EnsureConfigured();

            var builder = new StringBuilder();
            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                if (index > 0)
                    builder.AppendLine();
                builder.Append(index == selectedIndex ? "▶ " : "  ");
                builder.Append(ResolveLabel(snapshot.Entries[index].PlantTypeId.Value));
                builder.Append(": ");
                builder.Append(snapshot.Entries[index].Quantity);
            }

            stockText.text = builder.ToString();
            selectionText.text = snapshot.Entries.Count == 0
                ? "Нет доступных типов растений"
                : $"Выбрано: {ResolveLabel(snapshot.Entries[selectedIndex].PlantTypeId.Value)}\nКоличество: {requestedQuantity}";
            statusText.text = FormatStatus(lastResult);
        }

        private static string FormatStatus(SeedDispenseResult result)
        {
            if (result == null)
                return string.Empty;
            if (result.Succeeded)
                return result.IsReplay
                    ? "Запрос уже выполнен"
                    : $"Выдано: {result.Quantity}";

            switch (result.Rejection)
            {
                case SeedDispenseRejection.InsufficientStock:
                    return $"Недостаточно саженцев. Доступно: {result.AvailableQuantity}";
                case SeedDispenseRejection.MaterializationFailed:
                    return "Не удалось подготовить физические саженцы";
                case SeedDispenseRejection.InvalidQuantity:
                    return "Укажите корректное количество";
                case SeedDispenseRejection.QuantityLimitExceeded:
                    return "Запрошено слишком много саженцев за один раз";
                case SeedDispenseRejection.UnknownPlant:
                    return "Неизвестный тип растения";
                case SeedDispenseRejection.CommandConflict:
                    return "Конфликт повторного запроса";
                default:
                    return "Выдача отклонена";
            }
        }

        private void EnsureConfigured()
        {
            if (stockText == null || selectionText == null || statusText == null)
                throw new InvalidOperationException("Текстовое представление станции саженцев не настроено.");
        }

        private string ResolveLabel(string plantTypeId)
        {
            for (var index = 0; index < plantLabels.Length; index++)
            {
                if (string.Equals(plantLabels[index].PlantTypeId, plantTypeId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(plantLabels[index].Label))
                    return plantLabels[index].Label;
            }

            return plantTypeId;
        }
    }
}

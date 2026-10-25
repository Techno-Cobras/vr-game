using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VrGame.Data.Fertilizers;
using VrGame.Domain.Fertilizers;

namespace VrGame.Presentation.Fertilizers
{
    public sealed class FertilizerContainerTextView : MonoBehaviour, IFertilizerContainerView, IFertilizerApplicationView
    {
        [SerializeField] private Text stockText;
        [SerializeField] private Text statusText;
        public void Configure(Text stock, Text status) { stockText = stock ?? throw new ArgumentNullException(nameof(stock)); statusText = status ?? throw new ArgumentNullException(nameof(status)); }
        public void Render(FertilizerContainerSnapshot snapshot, int selectedIndex, FertilizerDispenseResult result)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (stockText == null || statusText == null) throw new InvalidOperationException("Fertilizer view не настроен.");
            var text = new StringBuilder();
            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                if (index > 0) text.AppendLine();
                var entry = snapshot.Entries[index];
                text.Append(index == selectedIndex ? "▶ " : "  ").Append(GradeLabel(entry.Grade))
                    .Append(" (x").Append(entry.Multiplier.ToString("0.##")).Append("): ").Append(entry.Quantity);
            }
            stockText.text = text.ToString();
            statusText.text = FormatStatus(result);
        }

        public void RenderApplication(FertilizerApplicationResult result)
        {
            if (statusText == null) throw new InvalidOperationException("Fertilizer view не настроен.");
            if (result == null) return;
            if (result.Succeeded)
            {
                statusText.text = result.IsReplay ? "Удобрение уже применено" : "Удобрение успешно применено";
                return;
            }

            switch (result.Rejection)
            {
                case FertilizerApplicationRejection.ItemIdentityMismatch:
                    statusText.text = "Это не выбранное удобрение";
                    break;
                case FertilizerApplicationRejection.ItemUnavailable:
                    statusText.text = "Удобрение отсутствует в inventory";
                    break;
                case FertilizerApplicationRejection.SlotRejected:
                    statusText.text = FormatSlotRejection(result.SlotRejection);
                    break;
                default:
                    statusText.text = "Применение удобрения отклонено";
                    break;
            }
        }
        private static string GradeLabel(FertilizerGrade grade) => grade == FertilizerGrade.Cheap ? "Обычное" : grade == FertilizerGrade.Medium ? "Улучшенное" : "Премиальное";
        private static string FormatSlotRejection(VrGame.Domain.Plants.PlantingSlotRejection rejection)
        {
            switch (rejection)
            {
                case VrGame.Domain.Plants.PlantingSlotRejection.FertilizerAlreadyApplied:
                    return "Это растение уже удобрено";
                case VrGame.Domain.Plants.PlantingSlotRejection.NotGrowing:
                    return "Можно удобрять только растущее растение";
                default:
                    return "Растение не принимает удобрение";
            }
        }
        private static string FormatStatus(FertilizerDispenseResult result)
        {
            if (result == null) return string.Empty;
            if (result.Succeeded) return result.IsReplay ? "Запрос уже выполнен" : "Удобрение выдано";
            switch (result.Rejection)
            {
                case FertilizerDispenseRejection.InsufficientStock: return "Удобрение закончилось";
                case FertilizerDispenseRejection.MaterializationFailed: return "Не удалось создать физический предмет";
                case FertilizerDispenseRejection.UnknownGrade: return "Неизвестный grade удобрения";
                case FertilizerDispenseRejection.CommandConflict: return "Конфликт повторного запроса";
                default: return "Выдача отклонена";
            }
        }
    }
}

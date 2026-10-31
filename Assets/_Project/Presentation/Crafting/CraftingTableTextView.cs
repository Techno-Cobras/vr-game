using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VrGame.Domain.Crafting;

namespace VrGame.Presentation.Crafting
{
    public sealed class CraftingTableTextView : MonoBehaviour, ICraftingTableView
    {
        [SerializeField] private Text recipesText;
        [SerializeField] private Text selectionText;
        [SerializeField] private Text requirementsText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button confirmButton;

        private static readonly Color SuccessColor = new Color(.35f, 1f, .45f);
        private static readonly Color MissingColor = new Color(1f, .78f, .2f);
        private static readonly Color FailureColor = new Color(1f, .32f, .28f);

        public void Configure(
            Text recipes,
            Text selection,
            Text requirements,
            Text status,
            Button confirm)
        {
            recipesText = recipes ?? throw new ArgumentNullException(nameof(recipes));
            selectionText = selection ?? throw new ArgumentNullException(nameof(selection));
            requirementsText = requirements ?? throw new ArgumentNullException(nameof(requirements));
            statusText = status ?? throw new ArgumentNullException(nameof(status));
            confirmButton = confirm ?? throw new ArgumentNullException(nameof(confirm));
        }

        public void Render(CraftingTableSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            EnsureConfigured();

            var list = new StringBuilder();
            for (var index = 0; index < snapshot.Recipes.Count; index++)
            {
                if (index > 0)
                    list.AppendLine();
                list.Append(index == snapshot.SelectedIndex ? "▶ " : "  ");
                list.Append(snapshot.Recipes[index].DisplayName);
            }
            recipesText.text = list.ToString();

            var selected = snapshot.SelectedRecipe;
            if (selected == null)
            {
                selectionText.text = "Рецепты отсутствуют";
                requirementsText.text = string.Empty;
                statusText.text = string.Empty;
                confirmButton.interactable = false;
                return;
            }

            selectionText.text =
                $"{selected.DisplayName}\nРезультат: {selected.OutputDisplayName} ×{selected.OutputQuantity}\nВ инвентаре: {selected.OwnedOutputQuantity}";
            var requirements = new StringBuilder("Ингредиенты:\n");
            foreach (var ingredient in selected.Ingredients)
            {
                requirements.Append(ingredient.IsAvailable ? "✓ " : "✕ ");
                requirements.Append(ingredient.DisplayName);
                requirements.Append(": ");
                requirements.Append(ingredient.AvailableQuantity);
                requirements.Append('/');
                requirements.Append(ingredient.RequiredQuantity);
                if (!ingredient.IsAvailable)
                {
                    requirements.Append(" (не хватает ");
                    requirements.Append(ingredient.MissingQuantity);
                    requirements.Append(')');
                }
                requirements.AppendLine();
            }
            requirementsText.text = requirements.ToString();
            confirmButton.interactable = selected.CanCraft;
            RenderStatus(snapshot);
        }

        private void RenderStatus(CraftingTableSnapshot snapshot)
        {
            statusText.color = Color.white;
            switch (snapshot.Feedback)
            {
                case CraftingTableFeedback.None:
                    statusText.text = snapshot.SelectedRecipe.CanCraft
                        ? "Ресурсы готовы"
                        : "Недостаточно ресурсов";
                    statusText.color = snapshot.SelectedRecipe.CanCraft ? Color.white : MissingColor;
                    return;
                case CraftingTableFeedback.MissingIngredients:
                    statusText.text = "Крафт заблокирован: проверьте нехватку ингредиентов";
                    statusText.color = MissingColor;
                    return;
                case CraftingTableFeedback.Success:
                    statusText.text = $"Создано: {snapshot.SelectedRecipe.OutputDisplayName} ×{snapshot.SelectedRecipe.OutputQuantity}";
                    statusText.color = SuccessColor;
                    return;
                case CraftingTableFeedback.Replay:
                    statusText.text = "Эта команда крафта уже выполнена";
                    statusText.color = SuccessColor;
                    return;
                default:
                    statusText.text = FormatFailure(snapshot.LastResult);
                    statusText.color = FailureColor;
                    return;
            }
        }

        private static string FormatFailure(CraftResult result)
        {
            if (result == null)
                return "Крафт отклонён";
            switch (result.Rejection)
            {
                case CraftRejection.UnknownRecipe:
                    return "Рецепт больше недоступен";
                case CraftRejection.MissingIngredients:
                    return "Недостаточно ингредиентов";
                case CraftRejection.OutputRejected:
                    return "Невозможно добавить результат в инвентарь";
                case CraftRejection.CommandConflict:
                    return "Конфликт повторной команды";
                default:
                    return "Крафт отклонён";
            }
        }

        private void EnsureConfigured()
        {
            if (recipesText == null || selectionText == null || requirementsText == null ||
                statusText == null || confirmButton == null)
                throw new InvalidOperationException("Текстовое представление Crafting Table не настроено.");
        }
    }
}

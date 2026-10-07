using UnityEditor;
using UnityEngine;
using VrGame.Presentation.Indicators;

namespace VrGame.Editor
{
    [CustomEditor(typeof(WorldSpaceIndicatorPresenter))]
    public sealed class WorldSpaceIndicatorPresenterEditor : UnityEditor.Editor
    {
        private IndicatorTarget previewTarget;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ручная VR-проверка", EditorStyles.boldLabel);
            previewTarget = (IndicatorTarget)EditorGUILayout.ObjectField(
                "Preview Target",
                previewTarget,
                typeof(IndicatorTarget),
                true);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || previewTarget == null))
            {
                var presenter = (WorldSpaceIndicatorPresenter)target;
                if (GUILayout.Button("Показать: нужна вода"))
                    presenter.Show(new IndicatorKey("manual.preview"), IndicatorVariant.Water, previewTarget);
                if (GUILayout.Button("Показать: готово к сбору"))
                    presenter.Show(new IndicatorKey("manual.preview"), IndicatorVariant.ReadyToHarvest, previewTarget);
                if (GUILayout.Button("Показать: доставка"))
                    presenter.Show(new IndicatorKey("manual.preview"), IndicatorVariant.Delivery, previewTarget);
                if (GUILayout.Button("Скрыть"))
                    presenter.Hide(new IndicatorKey("manual.preview"));
            }
        }
    }
}

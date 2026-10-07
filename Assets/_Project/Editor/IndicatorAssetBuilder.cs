using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VrGame.Presentation.Indicators;

namespace VrGame.Editor
{
    public static class IndicatorAssetBuilder
    {
        private const string Root = "Assets/_Project/Presentation/Indicators/Assets";
        private const string CatalogPath = Root + "/WorldSpaceIndicatorCatalog.asset";
        private const string PresenterPath = Root + "/WorldSpaceIndicatorPresenter.prefab";

        [MenuItem("VR Game/Создать assets индикаторов")]
        public static void Configure()
        {
            var water = CreateViewPrefab(
                "WaterIndicator",
                "~ НУЖНА ВОДА",
                new Color(0.02f, 0.22f, 0.55f, 0.94f));
            var ready = CreateViewPrefab(
                "ReadyToHarvestIndicator",
                "^ ГОТОВО К СБОРУ",
                new Color(0.12f, 0.42f, 0.08f, 0.94f));
            var delivery = CreateViewPrefab(
                "DeliveryIndicator",
                "> ДОСТАВКА",
                new Color(0.38f, 0.08f, 0.52f, 0.94f));

            var catalog = AssetDatabase.LoadAssetAtPath<WorldSpaceIndicatorCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorldSpaceIndicatorCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serializedCatalog = new SerializedObject(catalog);
            var definitions = serializedCatalog.FindProperty("definitions");
            definitions.arraySize = 3;
            ConfigureDefinition(definitions.GetArrayElementAtIndex(0), IndicatorVariant.Water, water);
            ConfigureDefinition(definitions.GetArrayElementAtIndex(1), IndicatorVariant.ReadyToHarvest, ready);
            ConfigureDefinition(definitions.GetArrayElementAtIndex(2), IndicatorVariant.Delivery, delivery);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            CreatePresenterPrefab(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("VR_GAME_INDICATORS_OK");
        }

        private static WorldSpaceIndicatorView CreateViewPrefab(string name, string text, Color color)
        {
            var path = $"{Root}/{name}.prefab";
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 100f);
            rect.localScale = Vector3.one * 0.001f;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            var canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(root.transform, false);
            Stretch(backgroundObject.GetComponent<RectTransform>());
            var background = backgroundObject.GetComponent<Image>();
            background.color = color;
            background.raycastTarget = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);
            Stretch(labelObject.GetComponent<RectTransform>());
            var label = labelObject.GetComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 42;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 28;
            label.resizeTextMaxSize = 42;
            label.color = Color.white;
            label.raycastTarget = false;

            var view = root.AddComponent<WorldSpaceIndicatorView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("label").objectReferenceValue = label;
            serializedView.FindProperty("background").objectReferenceValue = background;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<WorldSpaceIndicatorView>();
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreatePresenterPrefab(WorldSpaceIndicatorCatalog catalog)
        {
            var root = new GameObject("World Space Indicator Presenter");
            var presenter = root.AddComponent<WorldSpaceIndicatorPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("catalog").objectReferenceValue = catalog;
            serializedPresenter.FindProperty("prewarmPerVariant").intValue = 1;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PresenterPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void ConfigureDefinition(
            SerializedProperty property,
            IndicatorVariant variant,
            WorldSpaceIndicatorView prefab)
        {
            property.FindPropertyRelative("variant").enumValueIndex = (int)variant - 1;
            property.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            property.FindPropertyRelative("localOffset").vector3Value = new Vector3(0f, 0.35f, 0f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

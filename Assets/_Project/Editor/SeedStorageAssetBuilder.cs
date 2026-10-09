using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VrGame.Bootstrap;
using VrGame.Data.Items;
using VrGame.DataAssets;
using VrGame.DataAssets.Plants;
using VrGame.Presentation.SeedStorage;
using VrGame.VRInteraction.SeedStorage;

namespace VrGame.Editor
{
    internal static class SeedStorageAssetBuilder
    {
        private const string Root = "Assets/_Project/SeedStorage";
        private const string DataRoot = Root + "/Data";
        private const string PrefabRoot = Root + "/Prefabs";

        public static void BuildAndWire(Scene scene)
        {
            EnsureFolder(DataRoot);
            EnsureFolder(PrefabRoot);

            var basilSeedPrefab = CreateItemPrefab("Саженец базилика", PrefabRoot + "/BasilSeed.prefab", new Color(.3f, .8f, .25f));
            var mintSeedPrefab = CreateItemPrefab("Саженец мяты", PrefabRoot + "/MintSeed.prefab", new Color(.2f, .75f, .65f));
            var basilPlantPrefab = CreatePlantPrefab("Базилик", PrefabRoot + "/BasilPlant.prefab", new Color(.15f, .65f, .2f));
            var mintPlantPrefab = CreatePlantPrefab("Мята", PrefabRoot + "/MintPlant.prefab", new Color(.2f, .7f, .55f));

            var basilSeedRepresentation = CreateItemRepresentation("representation.seed.basil", basilSeedPrefab, DataRoot + "/BasilSeedRepresentation.asset");
            var mintSeedRepresentation = CreateItemRepresentation("representation.seed.mint", mintSeedPrefab, DataRoot + "/MintSeedRepresentation.asset");
            var basilHarvestRepresentation = CreateItemRepresentation("representation.harvest.basil", basilPlantPrefab, DataRoot + "/BasilHarvestRepresentation.asset");
            var mintHarvestRepresentation = CreateItemRepresentation("representation.harvest.mint", mintPlantPrefab, DataRoot + "/MintHarvestRepresentation.asset");

            var basilSeed = CreateItem("item.seed.basil", "Саженец базилика", ItemCategory.Seed, basilSeedRepresentation, DataRoot + "/BasilSeed.asset");
            var mintSeed = CreateItem("item.seed.mint", "Саженец мяты", ItemCategory.Seed, mintSeedRepresentation, DataRoot + "/MintSeed.asset");
            var basilHarvest = CreateItem("item.harvest.basil", "Урожай базилика", ItemCategory.HarvestedPlant, basilHarvestRepresentation, DataRoot + "/BasilHarvest.asset");
            var mintHarvest = CreateItem("item.harvest.mint", "Урожай мяты", ItemCategory.HarvestedPlant, mintHarvestRepresentation, DataRoot + "/MintHarvest.asset");
            var itemCatalog = CreateAsset<ItemCatalogAsset>(DataRoot + "/SeedStorageItemCatalog.asset");
            SetObjectArray(itemCatalog, "definitions", new UnityEngine.Object[] { basilSeed, mintSeed, basilHarvest, mintHarvest });

            var basilPlantRepresentation = CreatePlantRepresentation("representation.plant.basil", basilPlantPrefab, DataRoot + "/BasilPlantRepresentation.asset");
            var mintPlantRepresentation = CreatePlantRepresentation("representation.plant.mint", mintPlantPrefab, DataRoot + "/MintPlantRepresentation.asset");
            var basilPlant = CreatePlant("plant.basil", 60f, basilSeed, basilHarvest, basilPlantRepresentation, DataRoot + "/BasilPlant.asset");
            var mintPlant = CreatePlant("plant.mint", 75f, mintSeed, mintHarvest, mintPlantRepresentation, DataRoot + "/MintPlant.asset");
            var plantCatalog = CreateAsset<PlantCatalogAsset>(DataRoot + "/SeedStoragePlantCatalog.asset");
            SetObjectArray(plantCatalog, "definitions", new UnityEngine.Object[] { basilPlant, mintPlant });

            var stationPrefab = CreateStationPrefab();
            var slotPrefab = CreatePlantingSlotPrefab();
            var station = (GameObject)PrefabUtility.InstantiatePrefab(stationPrefab, scene);
            station.name = "Станция саженцев";
            station.transform.SetPositionAndRotation(new Vector3(-1.1f, 1.05f, 1.4f), Quaternion.Euler(0f, 25f, 0f));
            var slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, scene);
            slot.name = "Гнездо посадки";
            slot.transform.position = new Vector3(.8f, .85f, 1.35f);

            var issuedRoot = new GameObject("Выданные саженцы");
            issuedRoot.transform.position = new Vector3(-.65f, 1.05f, 1.25f);
            var compositionObject = new GameObject("Композиция станции саженцев");
            var composition = compositionObject.AddComponent<SeedStorageCompositionRoot>();
            var serialized = new SerializedObject(composition);
            serialized.FindProperty("itemCatalog").objectReferenceValue = itemCatalog;
            serialized.FindProperty("plantCatalog").objectReferenceValue = plantCatalog;
            serialized.FindProperty("materializer").objectReferenceValue = station.GetComponent<SeedBatchMaterializer>();
            serialized.FindProperty("stationController").objectReferenceValue = station.GetComponent<SeedStorageStationController>();
            serialized.FindProperty("stationView").objectReferenceValue = station.GetComponentInChildren<SeedStorageStationTextView>(true);
            serialized.FindProperty("issuedSeedsRoot").objectReferenceValue = issuedRoot.transform;
            serialized.FindProperty("plantingReceiver").objectReferenceValue = slot.GetComponent<SeedPlantingReceiver>();
            serialized.FindProperty("plantingSocket").objectReferenceValue = slot.GetComponent<XRSocketInteractor>();
            var initialStock = serialized.FindProperty("initialStock");
            initialStock.arraySize = 2;
            SetStock(initialStock.GetArrayElementAtIndex(0), "plant.basil", 5);
            SetStock(initialStock.GetArrayElementAtIndex(1), "plant.mint", 5);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateStationPrefab()
        {
            var root = new GameObject("Станция саженцев");
            root.AddComponent<SeedBatchMaterializer>();
            var controller = root.AddComponent<SeedStorageStationController>();
            var canvasObject = new GameObject(
                "Экран",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(TrackedDeviceGraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(520f, 420f);
            canvasRect.localScale = Vector3.one * .0015f;

            var stock = CreateText(canvasObject.transform, "Остатки", new Vector2(0f, 95f), new Vector2(480f, 150f), 26);
            var selection = CreateText(canvasObject.transform, "Выбор", new Vector2(0f, 5f), new Vector2(480f, 75f), 24);
            var status = CreateText(canvasObject.transform, "Статус", new Vector2(0f, -65f), new Vector2(480f, 55f), 22);
            var view = canvasObject.AddComponent<SeedStorageStationTextView>();
            var viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("stockText").objectReferenceValue = stock;
            viewSerialized.FindProperty("selectionText").objectReferenceValue = selection;
            viewSerialized.FindProperty("statusText").objectReferenceValue = status;
            var labels = viewSerialized.FindProperty("plantLabels");
            labels.arraySize = 2;
            SetLabel(labels.GetArrayElementAtIndex(0), "plant.basil", "Базилик");
            SetLabel(labels.GetArrayElementAtIndex(1), "plant.mint", "Мята");
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            var previous = CreateButton(canvasObject.transform, "Предыдущее", "◀", -190f);
            var next = CreateButton(canvasObject.transform, "Следующее", "▶", -95f);
            var less = CreateButton(canvasObject.transform, "Уменьшить", "−", 0f);
            var more = CreateButton(canvasObject.transform, "Увеличить", "+", 95f);
            var dispense = CreateButton(canvasObject.transform, "Выдать", "ВЫДАТЬ", 190f);
            UnityEventTools.AddPersistentListener(previous.onClick, controller.SelectPreviousPlant);
            UnityEventTools.AddPersistentListener(next.onClick, controller.SelectNextPlant);
            UnityEventTools.AddPersistentListener(less.onClick, controller.DecreaseQuantity);
            UnityEventTools.AddPersistentListener(more.onClick, controller.IncreaseQuantity);
            UnityEventTools.AddPersistentListener(dispense.onClick, controller.RequestDispenseSelected);

            var path = PrefabRoot + "/SeedStorageStation.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePlantingSlotPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Гнездо посадки";
            root.transform.localScale = new Vector3(.35f, .08f, .35f);
            var collider = root.GetComponent<Collider>();
            collider.isTrigger = true;
            root.AddComponent<Rigidbody>().isKinematic = true;
            root.AddComponent<XRSocketInteractor>();
            root.AddComponent<SeedPlantingReceiver>();
            var path = PrefabRoot + "/SeedPlantingSlot.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateItemPrefab(string name, string path, Color color)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = name;
            root.transform.localScale = Vector3.one * .12f;
            root.GetComponent<Renderer>().sharedMaterial = CreateMaterial(path.Replace(".prefab", ".mat"), color);
            root.AddComponent<Rigidbody>();
            root.AddComponent<XRGrabInteractable>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePlantPrefab(string name, string path, Color color)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = name;
            root.transform.localScale = new Vector3(.12f, .3f, .12f);
            root.GetComponent<Renderer>().sharedMaterial = CreateMaterial(path.Replace(".prefab", ".mat"), color);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ItemRepresentationAsset CreateItemRepresentation(string key, GameObject prefab, string path)
        {
            var asset = CreateAsset<ItemRepresentationAsset>(path);
            SetStringAndObject(asset, "key", key, "prefab", prefab);
            return asset;
        }

        private static PlantRepresentationAsset CreatePlantRepresentation(string key, GameObject prefab, string path)
        {
            var asset = CreateAsset<PlantRepresentationAsset>(path);
            SetStringAndObject(asset, "key", key, "prefab", prefab);
            return asset;
        }

        private static ItemDefinitionAsset CreateItem(string id, string displayName, ItemCategory category, ItemRepresentationAsset representation, string path)
        {
            var asset = CreateAsset<ItemDefinitionAsset>(path);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("category").intValue = (int)category;
            serialized.FindProperty("representation").objectReferenceValue = representation;
            serialized.FindProperty("maxStackSize").intValue = 16;
            serialized.FindProperty("allowsStackRepresentation").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static PlantDefinitionAsset CreatePlant(string id, float duration, ItemDefinitionAsset seed, ItemDefinitionAsset harvest, PlantRepresentationAsset representation, string path)
        {
            var asset = CreateAsset<PlantDefinitionAsset>(path);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("growthDurationSeconds").floatValue = duration;
            serialized.FindProperty("seedItem").objectReferenceValue = seed;
            serialized.FindProperty("harvestItem").objectReferenceValue = harvest;
            var stages = serialized.FindProperty("stages");
            stages.arraySize = 1;
            var stage = stages.GetArrayElementAtIndex(0);
            stage.FindPropertyRelative("startsAtNormalized").floatValue = 0f;
            stage.FindPropertyRelative("representation").objectReferenceValue = representation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, float x)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, -155f);
            rect.sizeDelta = new Vector2(85f, 65f);
            gameObject.GetComponent<Image>().color = new Color(.12f, .3f, .42f, .95f);
            var text = CreateText(gameObject.transform, "Надпись", Vector2.zero, rect.sizeDelta, 18);
            text.text = label;
            return gameObject.GetComponent<Button>();
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetStringAndObject(UnityEngine.Object target, string stringName, string value, string objectName, UnityEngine.Object reference)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(stringName).stringValue = value;
            serialized.FindProperty(objectName).objectReferenceValue = reference;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string propertyName, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStock(SerializedProperty property, string plantTypeId, int quantity)
        {
            property.FindPropertyRelative("plantTypeId").stringValue = plantTypeId;
            property.FindPropertyRelative("quantity").intValue = quantity;
        }

        private static void SetLabel(SerializedProperty property, string plantTypeId, string label)
        {
            property.FindPropertyRelative("plantTypeId").stringValue = plantTypeId;
            property.FindPropertyRelative("label").stringValue = label;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}

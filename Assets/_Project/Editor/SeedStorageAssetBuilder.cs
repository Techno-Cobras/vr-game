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
using VrGame.Data.Fertilizers;
using VrGame.DataAssets;
using VrGame.DataAssets.Fertilizers;
using VrGame.DataAssets.Plants;
using VrGame.Presentation.Indicators;
using VrGame.Presentation.Fertilizers;
using VrGame.Presentation.SeedStorage;
using VrGame.VRInteraction.Plants;
using VrGame.VRInteraction.Fertilizers;
using VrGame.VRInteraction.SeedStorage;
using VrGame.VRInteraction.Watering;

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
            var cheapFertilizerPrefab = CreateFertilizerPrefab("Обычное удобрение", PrefabRoot + "/CheapFertilizer.prefab", new Color(.65f, .48f, .22f));
            var mediumFertilizerPrefab = CreateFertilizerPrefab("Улучшенное удобрение", PrefabRoot + "/MediumFertilizer.prefab", new Color(.3f, .55f, .9f));
            var expensiveFertilizerPrefab = CreateFertilizerPrefab("Премиальное удобрение", PrefabRoot + "/ExpensiveFertilizer.prefab", new Color(.8f, .55f, .12f));
            var basilSproutPrefab = CreatePlantPrefab("Росток базилика", PrefabRoot + "/BasilSprout.prefab", new Color(.35f, .8f, .25f), .35f);
            var basilYoungPrefab = CreatePlantPrefab("Молодой базилик", PrefabRoot + "/BasilYoung.prefab", new Color(.22f, .72f, .2f), .65f);
            var basilPlantPrefab = CreatePlantPrefab("Базилик", PrefabRoot + "/BasilPlant.prefab", new Color(.15f, .65f, .2f), 1f);
            var mintSproutPrefab = CreatePlantPrefab("Росток мяты", PrefabRoot + "/MintSprout.prefab", new Color(.35f, .85f, .65f), .35f);
            var mintYoungPrefab = CreatePlantPrefab("Молодая мята", PrefabRoot + "/MintYoung.prefab", new Color(.25f, .78f, .6f), .65f);
            var mintPlantPrefab = CreatePlantPrefab("Мята", PrefabRoot + "/MintPlant.prefab", new Color(.2f, .7f, .55f), 1f);

            var basilSeedRepresentation = CreateItemRepresentation("representation.seed.basil", basilSeedPrefab, DataRoot + "/BasilSeedRepresentation.asset");
            var mintSeedRepresentation = CreateItemRepresentation("representation.seed.mint", mintSeedPrefab, DataRoot + "/MintSeedRepresentation.asset");
            var basilHarvestRepresentation = CreateItemRepresentation("representation.harvest.basil", basilPlantPrefab, DataRoot + "/BasilHarvestRepresentation.asset");
            var mintHarvestRepresentation = CreateItemRepresentation("representation.harvest.mint", mintPlantPrefab, DataRoot + "/MintHarvestRepresentation.asset");
            var cheapFertilizerRepresentation = CreateItemRepresentation("representation.fertilizer.cheap", cheapFertilizerPrefab, DataRoot + "/CheapFertilizerRepresentation.asset");
            var mediumFertilizerRepresentation = CreateItemRepresentation("representation.fertilizer.medium", mediumFertilizerPrefab, DataRoot + "/MediumFertilizerRepresentation.asset");
            var expensiveFertilizerRepresentation = CreateItemRepresentation("representation.fertilizer.expensive", expensiveFertilizerPrefab, DataRoot + "/ExpensiveFertilizerRepresentation.asset");

            var basilSeed = CreateItem("item.seed.basil", "Саженец базилика", ItemCategory.Seed, basilSeedRepresentation, DataRoot + "/BasilSeed.asset");
            var mintSeed = CreateItem("item.seed.mint", "Саженец мяты", ItemCategory.Seed, mintSeedRepresentation, DataRoot + "/MintSeed.asset");
            var basilHarvest = CreateItem("item.harvest.basil", "Урожай базилика", ItemCategory.HarvestedPlant, basilHarvestRepresentation, DataRoot + "/BasilHarvest.asset");
            var mintHarvest = CreateItem("item.harvest.mint", "Урожай мяты", ItemCategory.HarvestedPlant, mintHarvestRepresentation, DataRoot + "/MintHarvest.asset");
            var cheapFertilizer = CreateItem("item.fertilizer.cheap", "Обычное удобрение", ItemCategory.Fertilizer, cheapFertilizerRepresentation, DataRoot + "/CheapFertilizer.asset");
            var mediumFertilizer = CreateItem("item.fertilizer.medium", "Улучшенное удобрение", ItemCategory.Fertilizer, mediumFertilizerRepresentation, DataRoot + "/MediumFertilizer.asset");
            var expensiveFertilizer = CreateItem("item.fertilizer.expensive", "Премиальное удобрение", ItemCategory.Fertilizer, expensiveFertilizerRepresentation, DataRoot + "/ExpensiveFertilizer.asset");
            var itemCatalog = CreateAsset<ItemCatalogAsset>(DataRoot + "/SeedStorageItemCatalog.asset");
            SetObjectArray(itemCatalog, "definitions", new UnityEngine.Object[] { basilSeed, mintSeed, basilHarvest, mintHarvest, cheapFertilizer, mediumFertilizer, expensiveFertilizer });
            var cheapFertilizerDefinition = CreateFertilizer(FertilizerGrade.Cheap, cheapFertilizer, 1.25f, DataRoot + "/CheapFertilizerDefinition.asset");
            var mediumFertilizerDefinition = CreateFertilizer(FertilizerGrade.Medium, mediumFertilizer, 1.5f, DataRoot + "/MediumFertilizerDefinition.asset");
            var expensiveFertilizerDefinition = CreateFertilizer(FertilizerGrade.Expensive, expensiveFertilizer, 2f, DataRoot + "/ExpensiveFertilizerDefinition.asset");
            var fertilizerCatalog = CreateAsset<FertilizerCatalogAsset>(DataRoot + "/FertilizerCatalog.asset");
            SetObjectArray(fertilizerCatalog, "definitions", new UnityEngine.Object[] { cheapFertilizerDefinition, mediumFertilizerDefinition, expensiveFertilizerDefinition });

            var basilSproutRepresentation = CreatePlantRepresentation("representation.plant.basil.sprout", basilSproutPrefab, DataRoot + "/BasilSproutRepresentation.asset");
            var basilYoungRepresentation = CreatePlantRepresentation("representation.plant.basil.young", basilYoungPrefab, DataRoot + "/BasilYoungRepresentation.asset");
            var basilPlantRepresentation = CreatePlantRepresentation("representation.plant.basil", basilPlantPrefab, DataRoot + "/BasilPlantRepresentation.asset");
            var mintSproutRepresentation = CreatePlantRepresentation("representation.plant.mint.sprout", mintSproutPrefab, DataRoot + "/MintSproutRepresentation.asset");
            var mintYoungRepresentation = CreatePlantRepresentation("representation.plant.mint.young", mintYoungPrefab, DataRoot + "/MintYoungRepresentation.asset");
            var mintPlantRepresentation = CreatePlantRepresentation("representation.plant.mint", mintPlantPrefab, DataRoot + "/MintPlantRepresentation.asset");
            var basilPlant = CreatePlant(
                "plant.basil",
                60f,
                basilSeed,
                basilHarvest,
                new[] { basilSproutRepresentation, basilYoungRepresentation, basilPlantRepresentation },
                DataRoot + "/BasilPlant.asset");
            var mintPlant = CreatePlant(
                "plant.mint",
                75f,
                mintSeed,
                mintHarvest,
                new[] { mintSproutRepresentation, mintYoungRepresentation, mintPlantRepresentation },
                DataRoot + "/MintPlant.asset");
            var plantCatalog = CreateAsset<PlantCatalogAsset>(DataRoot + "/SeedStoragePlantCatalog.asset");
            SetObjectArray(plantCatalog, "definitions", new UnityEngine.Object[] { basilPlant, mintPlant });

            var stationPrefab = CreateStationPrefab();
            var slotPrefab = CreatePlantingSlotPrefab();
            var wateringCanPrefab = CreateWateringCanPrefab();
            var fertilizerContainerPrefab = CreateFertilizerContainerPrefab();
            var station = (GameObject)PrefabUtility.InstantiatePrefab(stationPrefab, scene);
            station.name = "Станция саженцев";
            station.transform.SetPositionAndRotation(new Vector3(-1.1f, 1.05f, 1.4f), Quaternion.Euler(0f, 25f, 0f));
            var slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, scene);
            slot.name = "Гнездо посадки";
            slot.transform.position = new Vector3(.8f, .85f, 1.35f);
            var wateringCan = (GameObject)PrefabUtility.InstantiatePrefab(wateringCanPrefab, scene);
            wateringCan.name = "Лейка";
            wateringCan.transform.SetPositionAndRotation(new Vector3(.15f, 1.05f, 1.15f), Quaternion.Euler(0f, 25f, 0f));
            var fertilizerContainer = (GameObject)PrefabUtility.InstantiatePrefab(fertilizerContainerPrefab, scene);
            fertilizerContainer.name = "Ящик удобрений";
            fertilizerContainer.transform.SetPositionAndRotation(new Vector3(-1.1f, .9f, .55f), Quaternion.Euler(0f, 25f, 0f));

            var presenterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Presentation/Indicators/Assets/WorldSpaceIndicatorPresenter.prefab");
            if (presenterPrefab == null)
                throw new InvalidOperationException("Prefab presenter индикаторов не найден.");
            var presenterObject = (GameObject)PrefabUtility.InstantiatePrefab(presenterPrefab, scene);
            presenterObject.name = "Индикаторы planting slot";
            var indicatorPresenter = presenterObject.GetComponent<WorldSpaceIndicatorPresenter>();
            var xrOrigin = UnityEngine.Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            var viewer = xrOrigin == null ? null : xrOrigin.Camera?.transform;
            if (viewer == null)
                throw new InvalidOperationException("XR camera для индикатора не найдена.");
            var presenterSerialized = new SerializedObject(indicatorPresenter);
            presenterSerialized.FindProperty("viewer").objectReferenceValue = viewer;
            presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

            var issuedRoot = new GameObject("Выданные саженцы");
            issuedRoot.transform.position = new Vector3(-.65f, 1.05f, 1.25f);
            var issuedFertilizersRoot = new GameObject("Выданные удобрения");
            issuedFertilizersRoot.transform.position = new Vector3(-.55f, 1.05f, .65f);
            var compositionObject = new GameObject("Композиция станции саженцев");
            var composition = compositionObject.AddComponent<SeedStorageCompositionRoot>();
            var serialized = new SerializedObject(composition);
            serialized.FindProperty("itemCatalog").objectReferenceValue = itemCatalog;
            serialized.FindProperty("plantCatalog").objectReferenceValue = plantCatalog;
            serialized.FindProperty("fertilizerCatalog").objectReferenceValue = fertilizerCatalog;
            serialized.FindProperty("materializer").objectReferenceValue = station.GetComponent<SeedBatchMaterializer>();
            serialized.FindProperty("stationController").objectReferenceValue = station.GetComponent<SeedStorageStationController>();
            serialized.FindProperty("stationView").objectReferenceValue = station.GetComponentInChildren<SeedStorageStationTextView>(true);
            serialized.FindProperty("issuedSeedsRoot").objectReferenceValue = issuedRoot.transform;
            serialized.FindProperty("plantingReceiver").objectReferenceValue = slot.GetComponent<SeedPlantingReceiver>();
            serialized.FindProperty("plantingSocket").objectReferenceValue = slot.GetComponent<XRSocketInteractor>();
            serialized.FindProperty("wateringReceiver").objectReferenceValue = slot.GetComponentInChildren<WateringReceiver>(true);
            serialized.FindProperty("wateringSource").objectReferenceValue = wateringCan.GetComponent<WateringSource>();
            serialized.FindProperty("indicatorSource").objectReferenceValue = slot.GetComponent<PlantingSlotIndicatorProjectionSource>();
            serialized.FindProperty("indicatorPresenter").objectReferenceValue = indicatorPresenter;
            serialized.FindProperty("fertilizerMaterializer").objectReferenceValue = fertilizerContainer.GetComponent<FertilizerItemMaterializer>();
            serialized.FindProperty("fertilizerController").objectReferenceValue = fertilizerContainer.GetComponent<FertilizerContainerController>();
            serialized.FindProperty("fertilizerView").objectReferenceValue = fertilizerContainer.GetComponentInChildren<FertilizerContainerTextView>(true);
            serialized.FindProperty("issuedFertilizersRoot").objectReferenceValue = issuedFertilizersRoot.transform;
            serialized.FindProperty("fertilizerReceiver").objectReferenceValue = slot.GetComponentInChildren<FertilizerApplicationReceiver>(true);
            var initialStock = serialized.FindProperty("initialStock");
            initialStock.arraySize = 2;
            SetStock(initialStock.GetArrayElementAtIndex(0), "plant.basil", 5);
            SetStock(initialStock.GetArrayElementAtIndex(1), "plant.mint", 5);
            var fertilizerStock = serialized.FindProperty("initialFertilizerStock");
            fertilizerStock.arraySize = 3;
            SetFertilizerStock(fertilizerStock.GetArrayElementAtIndex(0), FertilizerGrade.Cheap, 5);
            SetFertilizerStock(fertilizerStock.GetArrayElementAtIndex(1), FertilizerGrade.Medium, 5);
            SetFertilizerStock(fertilizerStock.GetArrayElementAtIndex(2), FertilizerGrade.Expensive, 5);
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
            var root = new GameObject("Гнездо посадки");
            var soil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            soil.name = "Почва";
            soil.transform.SetParent(root.transform, false);
            soil.transform.localScale = new Vector3(.35f, .08f, .35f);
            UnityEngine.Object.DestroyImmediate(soil.GetComponent<Collider>());
            var collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = .175f;
            collider.height = .16f;
            root.name = "Гнездо посадки";
            root.AddComponent<Rigidbody>().isKinematic = true;
            root.AddComponent<XRSocketInteractor>();
            root.AddComponent<SeedPlantingReceiver>();
            var visualRoot = new GameObject("Визуальные стадии").transform;
            visualRoot.SetParent(root.transform, false);
            visualRoot.localPosition = new Vector3(0f, .08f, 0f);
            var growthController = root.AddComponent<PlantGrowthController>();
            var growthSerialized = new SerializedObject(growthController);
            growthSerialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            growthSerialized.ApplyModifiedPropertiesWithoutUndo();

            var wateringZone = new GameObject("Зона полива");
            wateringZone.transform.SetParent(root.transform, false);
            wateringZone.transform.localPosition = new Vector3(0f, .2f, 0f);
            var wateringZoneCollider = wateringZone.AddComponent<BoxCollider>();
            wateringZoneCollider.isTrigger = true;
            wateringZoneCollider.size = new Vector3(.55f, .32f, .55f);
            var wateringReceiver = wateringZone.AddComponent<WateringReceiver>();
            var receiverSerialized = new SerializedObject(wateringReceiver);
            receiverSerialized.FindProperty("triggerZone").objectReferenceValue = wateringZoneCollider;
            receiverSerialized.ApplyModifiedPropertiesWithoutUndo();

            var fertilizerZone = new GameObject("Зона применения удобрения");
            fertilizerZone.transform.SetParent(root.transform, false);
            fertilizerZone.transform.localPosition = new Vector3(0f, .28f, 0f);
            var fertilizerCollider = fertilizerZone.AddComponent<SphereCollider>();
            fertilizerCollider.isTrigger = true;
            fertilizerCollider.radius = .32f;
            var fertilizerReceiver = fertilizerZone.AddComponent<FertilizerApplicationReceiver>();
            var fertilizerReceiverSerialized = new SerializedObject(fertilizerReceiver);
            fertilizerReceiverSerialized.FindProperty("triggerZone").objectReferenceValue = fertilizerCollider;
            fertilizerReceiverSerialized.ApplyModifiedPropertiesWithoutUndo();

            var indicatorAnchor = new GameObject("Якорь индикатора полива");
            indicatorAnchor.transform.SetParent(root.transform, false);
            indicatorAnchor.transform.localPosition = new Vector3(0f, .65f, 0f);
            var indicatorTarget = indicatorAnchor.AddComponent<IndicatorTarget>();
            var indicatorSource = root.AddComponent<PlantingSlotIndicatorProjectionSource>();
            var sourceSerialized = new SerializedObject(indicatorSource);
            sourceSerialized.FindProperty("target").objectReferenceValue = indicatorTarget;
            sourceSerialized.ApplyModifiedPropertiesWithoutUndo();
            var path = PrefabRoot + "/SeedPlantingSlot.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateWateringCanPrefab()
        {
            var root = new GameObject("Лейка");
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = .65f;
            var grab = root.AddComponent<XRGrabInteractable>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Корпус";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(.14f, .16f, .14f);
            body.GetComponent<Renderer>().sharedMaterial = CreateMaterial(
                PrefabRoot + "/WateringCan.mat",
                new Color(.16f, .48f, .78f));

            var outletObject = new GameObject("Носик — выход воды");
            outletObject.transform.SetParent(root.transform, false);
            outletObject.transform.localPosition = new Vector3(0f, .08f, .28f);
            outletObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var outletCollider = outletObject.AddComponent<CapsuleCollider>();
            outletCollider.radius = .045f;
            outletCollider.height = .22f;
            var outlet = outletObject.AddComponent<WateringOutlet>();

            var particlesObject = new GameObject("Струя успешного полива");
            particlesObject.transform.SetParent(outletObject.transform, false);
            particlesObject.transform.localPosition = new Vector3(0f, .11f, 0f);
            var particles = particlesObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .35f;
            main.startLifetime = .3f;
            main.startSpeed = 1.2f;
            main.startSize = .035f;
            main.maxParticles = 24;
            main.startColor = new Color(.3f, .75f, 1f, .9f);
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var source = root.AddComponent<WateringSource>();
            var outletSerialized = new SerializedObject(outlet);
            outletSerialized.FindProperty("source").objectReferenceValue = source;
            outletSerialized.FindProperty("outletCollider").objectReferenceValue = outletCollider;
            outletSerialized.ApplyModifiedPropertiesWithoutUndo();
            var sourceSerialized = new SerializedObject(source);
            sourceSerialized.FindProperty("interactable").objectReferenceValue = grab;
            sourceSerialized.FindProperty("outlet").objectReferenceValue = outlet;
            sourceSerialized.FindProperty("successParticles").objectReferenceValue = particles;
            sourceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var path = PrefabRoot + "/WateringCan.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateFertilizerContainerPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Ящик удобрений";
            root.transform.localScale = new Vector3(.65f, .45f, .35f);
            root.GetComponent<Renderer>().sharedMaterial = CreateMaterial(PrefabRoot + "/FertilizerContainer.mat", new Color(.26f, .18f, .1f));
            root.AddComponent<FertilizerItemMaterializer>();
            var controller = root.AddComponent<FertilizerContainerController>();
            var canvasObject = new GameObject("Экран удобрений", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, .62f, 0f);
            canvasObject.transform.localScale = new Vector3(1f / .65f, 1f / .45f, 1f / .35f);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(520f, 300f);
            canvasRect.localScale = Vector3.one * .0015f;
            var stock = CreateText(canvasObject.transform, "Остатки удобрений", new Vector2(0f, 55f), new Vector2(480f, 150f), 24);
            var status = CreateText(canvasObject.transform, "Статус", new Vector2(0f, -45f), new Vector2(480f, 55f), 21);
            var view = canvasObject.AddComponent<FertilizerContainerTextView>();
            var viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("stockText").objectReferenceValue = stock;
            viewSerialized.FindProperty("statusText").objectReferenceValue = status;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();
            var previous = CreateButton(canvasObject.transform, "Предыдущий grade", "◀", -130f);
            var next = CreateButton(canvasObject.transform, "Следующий grade", "▶", 0f);
            var dispense = CreateButton(canvasObject.transform, "Выдать удобрение", "ВЫДАТЬ", 130f);
            UnityEventTools.AddPersistentListener(previous.onClick, controller.SelectPreviousGrade);
            UnityEventTools.AddPersistentListener(next.onClick, controller.SelectNextGrade);
            UnityEventTools.AddPersistentListener(dispense.onClick, controller.RequestDispenseSelected);
            var path = PrefabRoot + "/FertilizerContainer.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateFertilizerPrefab(string name, string path, Color color)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = name;
            root.transform.localScale = new Vector3(.09f, .07f, .09f);
            root.GetComponent<Renderer>().sharedMaterial = CreateMaterial(path.Replace(".prefab", ".mat"), color);
            root.AddComponent<Rigidbody>();
            root.AddComponent<XRGrabInteractable>();
            root.AddComponent<FertilizerItemInstance>();
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

        private static GameObject CreatePlantPrefab(string name, string path, Color color, float scale)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = name;
            root.transform.localScale = new Vector3(.12f, .3f, .12f) * scale;
            root.GetComponent<Renderer>().sharedMaterial = CreateMaterial(path.Replace(".prefab", ".mat"), color);
            UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
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

        private static FertilizerDefinitionAsset CreateFertilizer(
            FertilizerGrade grade,
            ItemDefinitionAsset item,
            float multiplier,
            string path)
        {
            var asset = CreateAsset<FertilizerDefinitionAsset>(path);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("grade").intValue = (int)grade;
            serialized.FindProperty("item").objectReferenceValue = item;
            serialized.FindProperty("growthMultiplier").floatValue = multiplier;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static PlantDefinitionAsset CreatePlant(
            string id,
            float duration,
            ItemDefinitionAsset seed,
            ItemDefinitionAsset harvest,
            PlantRepresentationAsset[] representations,
            string path)
        {
            var asset = CreateAsset<PlantDefinitionAsset>(path);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("growthDurationSeconds").floatValue = duration;
            serialized.FindProperty("waterRequestMinNormalized").floatValue = .3f;
            serialized.FindProperty("waterRequestMaxNormalized").floatValue = .7f;
            serialized.FindProperty("seedItem").objectReferenceValue = seed;
            serialized.FindProperty("harvestItem").objectReferenceValue = harvest;
            var stages = serialized.FindProperty("stages");
            stages.arraySize = representations.Length;
            for (var index = 0; index < representations.Length; index++)
            {
                var stage = stages.GetArrayElementAtIndex(index);
                stage.FindPropertyRelative("startsAtNormalized").floatValue = index * .5f;
                stage.FindPropertyRelative("representation").objectReferenceValue = representations[index];
            }
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

        private static void SetFertilizerStock(SerializedProperty property, FertilizerGrade grade, int quantity)
        {
            property.FindPropertyRelative("grade").intValue = (int)grade;
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

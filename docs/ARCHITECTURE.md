# Архитектура проекта

## Текущее состояние

Репозиторий содержит запускаемый Unity VR project skeleton: зафиксированные
зависимости, Android/OpenXR/URP settings, одну стартовую сцену, XRI Starter
Assets, единый Input Action Asset, базовый grab, world-space UI,
EditMode/PlayMode tests и batchmode-команды smoke/build.

## Движок и целевая платформа

- Unity 6.3 LTS `6000.3.25f1`.
- Meta Quest 3 standalone: Android arm64, IL2CPP, Vulkan, 72 Гц.
- OpenXR Plugin `1.18.0` поверх Horizon OS OpenXR runtime.
- Input System `1.20.1`.
- XR Interaction Toolkit `3.6.1`.
- XR Plug-in Management `4.7.0`.
- URP `17.3.0`.

Выбор и ограничения зафиксированы в
[ADR 0005](adr/0005-vr-technology-stack.md). Точные версии находятся в
`Packages/manifest.json` и `Packages/packages-lock.json`.

## Структура репозитория

- `Assets/_Project/Bootstrap/`: composition root и lifecycle gate для Input Actions.
- `Assets/_Project/Data/`: engine-neutral DATA assembly без ссылок на UnityEngine.
- `Assets/_Project/DataAssets/`: Unity ScriptableObject adapters для преобразования
  авторских assets в неизменяемые DATA definitions и разрешения prefab representations.
- `Assets/_Project/Domain/`: engine-neutral DOMAIN assembly, зависящая только от DATA.
- `Assets/_Project/Input/VRControls.inputactions`: единственный versioned источник bindings.
- `Assets/_Project/Scenes/Startup.unity`: единственная сцена в Build Settings.
- `Assets/_Project/VRInteraction/`: XRI adapters; gameplay state здесь не хранится.
- `Assets/_Project/Presentation/`: UI presenters; gameplay state здесь не хранится.
- `Assets/_Project/Tests/`: EditMode и PlayMode tests.
- `Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/`: импортированный
  XRI baseline для XR Origin и controller-first interaction.
- `Assets/XR/`: versioned XR Management/OpenXR settings и Android loader.
- `Packages/`: зафиксированные Unity package зависимости.
- `ProjectSettings/`: versioned настройки Unity, Android, URP, input и build scene.
- `docs/adr/`: принятые архитектурные решения.
- `docs/REPOSITORY_POLICY.md`: правила metadata, LFS и serialized assets.
- `scripts/`: воспроизводимые проверки и Android build.

Локальные `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Build/` и
`Builds/` игнорируются. Авторские assets всегда коммитятся вместе с `.meta`.

## Стартовый поток и сцена

`Startup.unity` содержит ровно один XR Origin и один XR Interaction Manager.
XRI prefab создаёт tracked camera, left/right controller objects, direct/ray
interactors и locomotion providers. `VRControls.inputactions` подключён через
Input Action Manager и `VrBootstrap`; последний включает actions в `OnEnable`,
отключает в `OnDisable`/pause/focus loss и не владеет игровым состоянием.

В сцене также находятся пол с collider, Rigidbody + XR Grab Interactable,
Directional Light и интерактивный world-space Canvas через XR UI Input Module.
OpenXR loader автоматически инициализируется для Android; включены Meta Quest
Support и Oculus Touch Controller Profile.

## Границы систем

Независимые от движка границы MVP определены в
[ADR 0007](adr/0007-mvp-domain-boundaries.md). DATA предоставляет неизменяемые
валидированные definitions; DOMAIN / GAME LOGIC владеет авторитетным runtime
state; VR INTERACTION и UI / PRESENTATION преобразуют намерения и наблюдают
queries/events. Внешние слои могут вызывать доменные contracts, но Domain не
зависит от движка, VR, scene или UI.

Границы закреплены assembly definitions: DATA и DOMAIN используют
`noEngineReferences`, а VR INTERACTION, PRESENTATION и Bootstrap зависят от
домена только в направлении внутрь. У каждого изменяемого значения должен быть
один владелец; scene objects и presenters не являются хранилищами gameplay state.

Item definitions используют стабильные lowercase ASCII `ItemId`, отдельные
representation keys и валидированные stack rules. `ItemCatalog` копирует и
сортирует definitions по ID, отклоняет дубли и выполняет ordinal case-sensitive
lookup. Unity data assets строят этот каталог один раз и держат prefab resolver
во внешнем adapter-слое; display name, asset name, prefab и scene object никогда
не используются как identity предмета.

Plant definitions в DATA используют стабильные `PlantTypeId`, длительность
роста, seed/harvest `ItemId` и строго возрастающие нормализованные стадии с
отдельными representation keys. `PlantCatalog` проверяет принадлежность items
runtime-каталогу и их категории, копирует definitions и выдаёт их в
детерминированном порядке. Unity data assets содержат только авторские данные;
`RuntimePlantCatalog` отдельно разрешает representation key в prefab и не хранит
прогресс роста.

Runtime-инвентарь реализован engine-neutral aggregate в DOMAIN. Один и тот же
контракт используется для игрока и контейнеров, хранит только `ItemId -> quantity`
и версию, отклоняет неизвестные items, недостаточное количество и overflow.
Batch consumption нормализует повторяющиеся IDs и фиксирует все изменения одной
версией либо не меняет ничего. После успешной фиксации публикуется одно
`InventoryChanged` с reason, correlation ID и immutable before/after/delta,
достаточными для UI projection. Event sink использует non-throwing `TryPublish`:
результат mutation всегда возвращает committed event и флаг доставки, поэтому
ошибка adapter не провоцирует повтор mutation, а transaction coordinator может
буферизовать публикацию до общего commit. Физические objects и UI в aggregate
не входят. Синхронная повторная mutation из event sink типизированно отклоняется,
чтобы observers всегда видели монотонный порядок aggregate versions.

Централизованный `EconomyService` в DOMAIN единолично владеет неотрицательным
целочисленным балансом. `Credit` и `TrySpend` используют reason, `CommandId` и
`CorrelationId`, защищены от overflow, overdraft и повторного входа. Завершённые
успешные и отклонённые команды кэшируются до конца сессии: повтор возвращает
прежний результат без второй mutation или события, а конфликтующий payload
отклоняется. Каждая успешная команда добавляет immutable `BalanceChanged` в
read-only ledger и использует тот же безопасный `TryPublish` contract.
Частый economy snapshot содержит только balance/version; копирование ledger
выполняется лишь отдельным явным audit-запросом, чтобы не создавать растущие
per-frame allocations в VR presenters.

World-space indicators реализованы в PRESENTATION как event-driven projections.
Presenter наблюдает `IIndicatorProjectionSource`, восстанавливает актуальный
snapshot при включении, принимает state events и использует стабильный
presentation key, prefab catalog и внутренний pool. Низкоуровневые операции
`Show`, `Rebind`, `Reconcile` и `Hide` оставлены для узких system adapters. Явный `IndicatorTarget`
сообщает о disable/destroy без scene searches, а cached viewer используется
только для billboard активных views. Water, ready-to-harvest и delivery имеют
разные контрастные prefabs; gameplay state и domain event bus presenter не
создаёт — plant/delivery adapters передают ему восстановимую projection state.

## Зависимости

| Package | Version |
| --- | --- |
| Input System | `1.20.1` |
| Universal Render Pipeline | `17.3.0` |
| XR Interaction Toolkit | `3.6.1` |
| XR Plug-in Management | `4.7.0` |
| OpenXR Plugin | `1.18.0` |
| Unity Test Framework | `1.6.0` |

## Сборка и проверки

Команды выполняются из корня репозитория в PowerShell:

```powershell
pwsh -NoProfile -File scripts/Test-RepositoryPolicy.ps1
pwsh -NoProfile -File scripts/Test-UnitySmoke.ps1
pwsh -NoProfile -File scripts/Test-UnityProject.ps1
pwsh -NoProfile -File scripts/Build-Android.ps1
```

`Test-UnitySmoke.ps1` проверяет сцену, XR rig, grab, UI, bindings и Build
Settings. `Test-UnityProject.ps1` запускает EditMode и PlayMode tests и пишет
результаты в игнорируемый `Logs/TestResults/`. `Build-Android.ps1` создаёт
игнорируемый `Builds/Android/vr-game.apk` для arm64/IL2CPP/Vulkan.

Runner определяет Editor требуемой версии через конфигурацию Unity Hub и
стандартные каталоги. Для CI и нестандартных установок поддерживаются параметр
`-UnityEditor` и переменная окружения `UNITY_EDITOR`.

Проверенный workflow и ограничения зафиксированы в
[отчёте задачи #8](smoke/vr8-project-skeleton.md).

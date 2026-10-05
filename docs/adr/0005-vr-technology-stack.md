# ADR 0005: Движок, XR, ввод и система взаимодействий

- Статус: принято
- Дата: 2026-10-05
- Связанная задача: [#5](https://github.com/Techno-Cobras/vr-game/issues/5)
- Связанное решение: [ADR 0007](0007-mvp-domain-boundaries.md)

## Контекст

Репозиторий пока не содержит игрового проекта, движка, манифеста зависимостей или
XR-настроек. Для создания проверяемого каркаса в задаче #8 необходимо заранее
выбрать один воспроизводимый стек и закрыть вопросы целевого устройства,
рендеринга, ввода, физики и жизненного цикла приложения.

MVP включает физический захват предметов и инструментов, посадочные слоты,
world-space UI, haptic feedback, планшет, доску заказов, Crafting Table и
защищённые от повторного ввода транзакции. ADR 0007 требует, чтобы движок и VR
оставались внешними адаптерами, а DOMAIN / GAME LOGIC не зависел от них.

## Критерии выбора

Стек оценивается по следующим критериям:

1. официальная поддержка standalone VR на Meta Quest 3 через OpenXR;
2. готовые controller input, grab, socket, haptics, locomotion и world-space UI;
3. пригодность для мобильного VR и стабильная частота кадров;
4. возможность изолировать чистый domain-код от scene-компонентов;
5. фиксируемые версии движка и пакетов с длительным сроком поддержки;
6. воспроизводимая сборка Android arm64 и тестирование на физическом устройстве;
7. приемлемая сложность для небольшой команды и параллельной разработки.

## Рассмотренные варианты

| Вариант | Преимущества | Риски для этого MVP | Решение |
| --- | --- | --- | --- |
| Unity 6.3 LTS | Официальные OpenXR и Quest workflows; XR Interaction Toolkit предоставляет controller input, grab, haptics, Canvas UI, XR Origin и locomotion; C# позволяет держать domain вне `MonoBehaviour` | Unity YAML, `.meta`, scenes и prefabs требуют отдельной repository policy; Vulkan нужно проверять на устройстве | Выбран |
| Unreal Engine 5.8 | Встроенный OpenXR; VR Template содержит grab, teleport и snap turn; Quest 2/3 проверены Epic на Android | Более тяжёлый standalone-профиль; значительная часть проекта хранится в бинарных `.uasset`; до gameplay требуется больше решений по структуре Blueprint/C++ и ownership | Не выбран |
| Godot 4.7.2 | Встроенный OpenXR, открытый исходный код, Mobile renderer рекомендован для Quest 3 | Базовые interactions требуют XR Tools, а standalone — отдельный vendor plugin; версии engine/addons добавляют интеграционный риск на foundation-этапе | Не выбран |

Unity выбран не как владелец gameplay-архитектуры, а как внешний runtime и набор
адаптеров. Все правила владения состоянием из ADR 0007 сохраняются.

## Принятый стек

### Точные версии

| Компонент | Зафиксированное значение | Правило обновления |
| --- | --- | --- |
| Unity Editor | Unity 6.3 LTS `6000.3.25f1` | Любое изменение версии — отдельный PR с повторным smoke и обновлением ADR/архитектуры |
| XR provider | `com.unity.xr.openxr` `1.18.0` | Только стабильный release, зафиксированный в `Packages/manifest.json` и lock-файле |
| Input System | `com.unity.inputsystem` `1.20.1` | Единственный input backend; старый Input Manager не используется |
| Interaction framework | `com.unity.xr.interaction.toolkit` `3.6.1` | Starter Assets разрешены только как исходная конфигурация с review импортированных assets |
| XR lifecycle/configuration | `com.unity.xr.management` `4.7.0` | Автоматическая инициализация loader на старте |
| OpenXR API | OpenXR 1.1, актуальная спецификация `1.1.63` на дату решения | Использовать только доступные у runtime core/extension capabilities |
| Render pipeline | URP из Unity 6.3 LTS | Задача #8 фиксирует точную разрешённую Editor-ом версию в manifest/lock; Built-in RP и HDRP не используются |

Версии пакетов проверены в официальном Unity Registry; выбранные releases
совместимы с Unity `6000.0` или новее. Preview-пакеты запрещены без отдельного
ADR или явного изменения этого решения.

### Целевое устройство и runtime

- Единственный обязательный shipping target MVP: **Meta Quest 3 standalone**.
- Контроллеры: Meta Quest Touch Plus; interaction model — controller-first.
- Hand tracking, passthrough/MR, eye tracking и face tracking находятся вне MVP.
- Build target: Android arm64, IL2CPP, Vulkan.
- Android: `minSdkVersion = 32`, `targetSdkVersion = 34`,
  `compileSdkVersion = 34`.
- XR runtime provider: системный **Horizon OS OpenXR runtime**.
- Минимальная версия Horizon OS для выбранного Unity OpenXR loader: v65.
- Quest Link и Meta XR Simulator разрешены для разработки, но не заменяют
  проверку standalone-сборки на физическом Quest 3.
- PCVR, SteamVR, Quest 2 и Quest 3S не являются обязательными shipping targets.

Версия системного runtime не управляется Unity Package Manager и обновляется
Meta вместе с Horizon OS. Поэтому её нельзя закрепить в `manifest.json`. Каждый
hardware test report обязан указывать модель шлема, точный Horizon OS build,
доступную версию OpenXR runtime и версию APK. Это управляемая внешняя зависимость,
а не незакрытый выбор стека.

Дополнительные Meta XR SDK и `com.unity.xr.meta-openxr` не входят в начальный
набор: MVP не требует vendor-specific MR-функций. Их добавление допустимо только
при конкретной потребности, отдельном version pin и подтверждении, что domain не
начинает зависеть от Meta API. Устаревающий Oculus XR Plugin не используется.

## Рендеринг и бюджет кадра

- Базовая частота MVP: **72 Гц**, период одного кадра — **13,9 мс**. CPU frame
  time и GPU frame time контролируются отдельно и каждый должен укладываться в
  этот период с запасом; их нельзя складывать как последовательные расходы.
- 90 Гц (11,1 мс) разрешается только после профилирования полного сценария E на
  физическом Quest 3. 120 Гц (8,3 мс) не является целью MVP.
- Используется URP с single-pass instanced/multiview rendering.
- Forward rendering, мобильные shaders, baked lighting и ограниченное число
  real-time lights — стартовый профиль. HDRP и desktop-only effects запрещены.
- Производительность оценивается на устройстве, а не только в Editor/Quest Link.
- Каждая сцена должна избегать необязательных per-frame searches, allocations и
  дублирующих камер/Audio Listener.

Meta Horizon Store требует устойчиво выдерживать запрошенную refresh rate.
Невыполнение бюджета считается функциональным дефектом VR, а не косметической
оптимизацией.

## Физика и время

- Начальный `Time.fixedDeltaTime` — `1 / 72` секунды, чтобы физический grab и
  collisions обновлялись с базовой частотой дисплея.
- `Maximum Allowed Timestep` должен быть ограничен в #8 и проверен на устройстве,
  чтобы перегрузка не вызывала длинную цепочку догоняющих physics steps.
- Forces, Rigidbody movement, collision и физический grab выполняются в fixed
  phase; считывание input intent — в dynamic phase; поздняя корректировка tracked
  pose — в предусмотренной XRI фазе `OnBeforeRender`.
- Domain timers растений, заказов и доставки не используют physics tick. Они
  работают через внедрённые clock/time source из ADR 0007.
- Collision callback может создать не более одной domain command с `CommandId`;
  повторный callback не повторяет расход, награду, покупку или сбор.
- Настройка `1 / 72` является стартовой гипотезой. Если device profile покажет
  неприемлемую CPU-нагрузку, её изменение требует измерений сценария E и
  документированного решения, а не скрытой правки Project Settings.

## Input и interaction lifecycle

- В проекте существует один versioned Input Action Asset — источник bindings.
- Gameplay adapters используют action-based XRI и Input System; прямое чтение
  legacy Input Manager запрещено.
- Action Maps подписываются и включаются в `OnEnable`, отключаются и полностью
  отписываются в `OnDisable`.
- Быстрый или повторный input блокируется в adapter и остаётся защищённым
  session-scoped `CommandId` в domain, как определено ADR 0007.
- Physical interaction сообщает намерение через command API и не меняет
  Inventory, Planting Slot, Order, Economy или Delivery Queue напрямую.
- Один persistent XR Origin и один composition root создают adapters и presenters;
  additive scenes не создают вторую XR rig или второй authoritative service.

## Жизненный цикл приложения и XR

1. XR Plug-in Management инициализирует OpenXR loader до создания gameplay
   session.
2. Composition root создаёт domain session только после успешной XR
   initialization; ошибка даёт диагностическое состояние без частичного старта.
3. При `OnApplicationPause(true)` приложение отключает gameplay/UI Action Maps,
   прекращает interaction commands, замораживает session clock и освобождает
   transient grab state без изменения authoritative inventory/domain state.
4. При потере input focus tracked controllers/hands скрываются, а входные
   действия игнорируются. Потеря tracking или focus не создаёт domain command.
5. После resume приложение ждёт валидный tracking, восстанавливает projections из
   domain queries и только затем включает Action Maps.
6. Уже удерживаемая при resume кнопка не считается новым нажатием и не повторяет
   purchase, craft, delivery или harvest.
7. На Android нельзя полагаться на quit callback: процесс может быть завершён
   после pause. Всё обязательное cleanup выполняется идемпотентно в pause/disable.

## Сопоставление с ADR 0007

| Слой ADR 0007 | Реализация в выбранном стеке |
| --- | --- |
| DATA | Чистые сериализуемые definitions и каталоги; Unity assets адаптируются в engine-neutral records |
| DOMAIN / GAME LOGIC | Обычные C# assemblies без ссылок на `UnityEngine`, XRI или Meta packages |
| VR INTERACTION | XRI Interactors/Interactables, Input Actions, Rigidbody/collision adapters |
| UI / PRESENTATION | World-space Canvas/presenters, которые читают queries/events и отправляют commands |
| Composition root | Единственная startup scene с XR Origin и явным wiring; gameplay-правил не содержит |

Assembly definitions в #8 должны запретить ссылку Domain assembly на Unity
presentation/interaction assemblies. Scene object, `MonoBehaviour`, Scriptable
Object, prefab и physical representation не становятся источником domain state.

## Проверка сценариев MVP

| Сценарий | Возможности выбранного стека | Обязательная проверка |
| --- | --- | --- |
| A: выращивание | Direct grab, sockets/slots, Rigidbody interactions, haptics, indicators | Повторные collisions не дублируют посадку/полив/удобрение/сбор |
| B: крафт | World-space Canvas, ray/direct interaction, action-based confirmation | Быстрый input создаёт одну craft command |
| C: заказ | World-space Order Board, waypoint presenter, grab delivery | Wrong/partial/repeated delivery не меняет domain state |
| D: магазин | Переносной world-space UI, balance presenter, Delivery Box slots | Purchase и claim идемпотентны после pause/resume |
| E: полный цикл | Все перечисленные adapters поверх одного domain session | 72 FPS, точные quantities/balance и отсутствие ручной подмены state |

## Риски и меры контроля

| Риск | Мера контроля |
| --- | --- |
| В known issues Unity `6000.3.25f1` указан Vulkan SIGSEGV при `vkAcquireNextImageKHR == VK_TIMEOUT` (`UUM-153744`) | #8 выполняет повторные cold start, suspend/resume и несколько запусков APK на Quest 3; откат на `6000.3.24f1` допустим только при воспроизведении на `.25`, подтверждённом отсутствии дефекта на `.24`, отдельном PR и обновлении ADR/архитектурной карты |
| Runtime Horizon OS обновляется независимо | Записывать OS/runtime build в каждый device report; повторять smoke после обновления шлема |
| Unity scene/prefab/settings conflicts | Задача #6 вводит ignore, metadata/LFS и ownership policy до добавления skeleton |
| XRI sample assets могут принести лишние настройки | Импортировать только нужные Starter Assets и review каждого versioned asset |
| Editor/Link скрывает mobile bottlenecks | Performance acceptance измеряется на standalone Quest 3 |
| Потеря focus создаёт повторный input | Lifecycle gate + adapter debounce + domain idempotency |

## Checklist для bootstrap-задачи #8

- установить Unity `6000.3.25f1` через Hub с Android Build Support, SDK, NDK и JDK;
- подтвердить доступную Unity license и возможность batch mode;
- подтвердить Meta developer organization, developer mode и signing setup;
- иметь физический Quest 3 для финального smoke; без него #8 не считается
  полностью проверенной;
- создать URP-проект и закоммитить `ProjectVersion.txt`, `manifest.json` и
  `packages-lock.json` с версиями из этого ADR;
- настроить Android arm64, IL2CPP, Vulkan и SDK levels;
- включить OpenXR для Android, Meta Quest Support и controller interaction
  profile; не включать Oculus XR Plugin;
- создать одну startup scene с XR Origin и composition root;
- импортировать минимальный XRI Starter Assets/Input Action Asset;
- добавить assembly boundaries по ADR 0007;
- проверить запуск, controller tracking, grab, world-space UI и haptics;
- выполнить несколько cold starts и pause/resume циклов на Quest 3;
- записать Editor, package, Horizon OS/runtime и APK versions в smoke report;
- профилировать стабильные 72 FPS и frame budget на target.

Недоступность лицензии, Meta organization/signing или физического Quest 3 — это
операционный blocker для завершения #8, но не неоднозначность архитектурного
выбора: требуемые target и prerequisites здесь определены полностью.

## Нецели

- создание Unity project, scene, prefab или package manifest в этой задаче;
- реализация gameplay или VR adapters;
- поддержка hand tracking, MR/passthrough, multiplayer, persistence или PCVR;
- автоматическое обновление Editor/packages без отдельного review;
- обещание поддержки устройства, не прошедшего физический device test.

## Последствия

- Задача #8 получает точный воспроизводимый baseline и критерии smoke/performance.
- Задача #6 может определить Unity-specific metadata, LFS и ownership policy.
- Gameplay-команда получает готовые VR primitives, сохраняя domain независимым.
- Команда принимает стоимость Unity YAML/meta discipline и обязательного device
  profiling.
- Изменение движка, обязательного target, XR provider или interaction framework
  требует ADR, который заменяет это решение.

## Официальные источники

- [Unity 6000.3.25f1](https://unity.com/releases/editor/whats-new/6000.3.25f1)
- [Поддержка Unity 6 LTS](https://unity.com/releases/unity-6/support)
- [XR Interaction Toolkit 3.6.1](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.6/manual/index.html)
- [Input System 1.20.1](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/index.html)
- [Unity Package Registry: OpenXR](https://packages.unity.com/com.unity.xr.openxr)
- [Unity Package Registry: XR Plug-in Management](https://packages.unity.com/com.unity.xr.management)
- [Khronos OpenXR 1.1 Registry](https://registry.khronos.org/OpenXR/)
- [Meta: Unity OpenXR Plugin](https://developers.meta.com/vr/documentation/unity/unity-xr-plugin/)
- [Meta: Quest OpenXR settings](https://developers.meta.com/horizon/documentation/unity/unity-openxr-settings-quest/)
- [Meta: hardware/software requirements](https://developers.meta.com/vr/documentation/unity/unity-development-requirements/)
- [Meta: application lifecycle](https://developers.meta.com/vr/documentation/unity/unity-lifecycle/)
- [Meta: display refresh rates](https://developers.meta.com/vr/documentation/unity/unity-set-disp-freq/)
- [Unreal Engine 5.8 VR Template](https://dev.epicgames.com/documentation/unreal-engine/vr-template-in-unreal-engine)
- [Unreal Engine 5.8 OpenXR](https://dev.epicgames.com/documentation/en-us/unreal-engine/developing-for-head-mounted-experiences-with-openxr-in-unreal-engine)
- [Godot XR setup](https://docs.godotengine.org/en/stable/tutorials/xr/setting_up_xr.html)
- [Godot releases](https://godotengine.org/download/archive/)

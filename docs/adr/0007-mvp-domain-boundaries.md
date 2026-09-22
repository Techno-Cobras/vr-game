# ADR 0007: Границы доменов MVP и владение состоянием

- Статус: принято
- Дата: 2026-09-22
- Владельцы решения: участники, отвечающие за архитектуру gameplay
- Связанная задача: [#7](https://github.com/Techno-Cobras/vr-game/issues/7)

## Контекст

Gameplay-системы MVP будут разрабатываться параллельно до того, как в репозитории
появятся выбранный движок и XR-фреймворк. Им нужны стабильные, независимые от
движка границы, чтобы scene object, UI presenter или интеграционный сервис не
могли стать вторым источником истины.

Это решение охватывает предметы, инвентарь, растения, крафт, заказы, экономику,
магазин, доставку и индикаторы. Оно определяет владение состоянием и контракты,
но не конкретные классы, engine assets, сохранение, многопоточность или
реализацию event bus.

В соответствии с терминологией задач [#13](https://github.com/Techno-Cobras/vr-game/issues/13),
[#15](https://github.com/Techno-Cobras/vr-game/issues/15) и
[#32](https://github.com/Techno-Cobras/vr-game/issues/32), слово «саженец» в этом
ADR обозначает предмет `seed` в инвентаре, а не уже посаженный экземпляр растения.

## Решение

### Слои и направление зависимостей

| Слой | Ответственность | Допустимые зависимости |
| --- | --- | --- |
| DATA | Неизменяемые валидированные definitions и каталоги | Стабильные value types и другие DATA definitions |
| DOMAIN / GAME LOGIC | Aggregates, сервисы, command handlers, queries, events и transaction coordinators | DATA-контракты и узкие независимые от runtime порты: время, случайность, event sink и session transaction |
| VR INTERACTION | Преобразует input, grab, collision и действия инструментов в одну domain command; материализует физические representations | Публичные domain commands, queries, results и events |
| UI / PRESENTATION | Read models, world-space indicators, feedback и экраны | Публичные domain queries, results и events; commands — только для выражения намерения игрока |

Composition root может видеть все слои для создания объектов и связывания их
lifecycle, но не содержит gameplay-правил.

```text
VR INTERACTION -----+
                    +--> DOMAIN / GAME LOGIC --> DATA
UI / PRESENTATION --+             |
       ^                           |
       +------- results/events ----+
```

Запрещены следующие зависимости:

- DOMAIN или DATA от типов движка, XR, scene, VR или UI;
- прямая запись UI- или VR-адаптеров в aggregate или state store;
- доступ одной domain-системы к внутреннему состоянию другой;
- изменяемое runtime-состояние внутри DATA definitions;
- использование events как скрытых синхронных commands между владельцами состояния.

Cross-system workflow использует именованный transaction coordinator и узкие
публичные контракты владельцев. Coordinator владеет только orchestration и
idempotency; он не копирует balance, quantities, lifecycle или queue, работу с
которыми координирует.

### Владение состоянием

У каждого изменяемого поля есть ровно один авторитетный владелец.

| Система | Авторитетный владелец | Состояние во владении | Что явно не является источником истины |
| --- | --- | --- | --- |
| Предметы | Item Catalog | Неизменяемые item definitions, категории, stack rules и representation keys | Display names, scene objects и физические экземпляры предметов |
| Инвентарь | Один Inventory aggregate на игрока или контейнер, доступный через Inventory Service | `ItemId -> quantity`, опциональная capacity и версия aggregate | Физические объекты, UI контейнера, crafting, shop и delivery presenters |
| Растения | Один Planting Slot aggregate на слот; Plant Catalog владеет только definitions | Occupancy, plant type, lifecycle, elapsed/progress, water threshold/request, fertilizer modifier и версия | Визуал растения, индикаторы, инструменты и общие plant definitions |
| Крафт | Recipe Catalog владеет definitions; Crafting Service координирует транзакцию | Только ограниченное сессией состояние command idempotency/in-flight при необходимости | Quantities ингредиентов и результата — ими продолжает владеть Inventory |
| Заказы | Order Service и его Order aggregates | Созданные заказы, lifecycle, identity активного заказа и инвариант единственного активного заказа | Order Board, waypoint, quantities инвентаря и деньги |
| Экономика | Economy Service | Неотрицательный целочисленный balance, ledger entries с причиной и command idempotency | UI-текст, заказы, магазин и конфигурация наград |
| Магазин | Shop Catalog владеет offers; Purchase Service координирует транзакцию | Неизменяемые offers и ограниченная сессией idempotency purchase commands соответственно | Balance и delivery entries |
| Доставка | Delivery Queue | FIFO entries, item, quantity, status, порядок, версия и claim state | Слоты Delivery Box и созданные физические объекты |
| Индикаторы | Indicator Presenter | Visual handle, привязанный target и отображаемый variant | Потребность в воде, готовность к сбору, активный заказ и состояние доставки |

Вычисляемые значения не дублируют состояние. Например, `ReadyForDelivery` — это
query над активным заказом и inventory, а видимость индикатора — projection,
которую можно восстановить из queries и events.

### Стабильные идентификаторы

Идентификаторы definitions строго типизированы и задаются автором данных,
например `ItemId`, `PlantTypeId`, `RecipeId`, `OrderTemplateId` и `ShopOfferId`.
Runtime-идентификаторы включают `InventoryId`, `PlantingSlotId`, `OrderId` и
`DeliveryEntryId`.

- Сериализованные definition IDs используют канонические lowercase ASCII
  namespaced-значения, например `item.seed.basil`, и ordinal case-sensitive
  comparison.
- ID уникален внутри своего типа, неизменяем после публикации и никогда не
  переиспользуется с другим смыслом.
- Display names, позиции в коллекции, пути файловой системы, scene paths и
  engine instance IDs никогда не являются domain identifiers.
- Валидация каталогов отклоняет дубли ID, отсутствующие ссылки, несовместимые
  категории и некорректные definitions до начала gameplay.
- Runtime IDs уникальны в пределах сессии. Persistence и миграции сохранений
  остаются вне scope MVP, пока не будут спроектированы отдельно.
- Commands, подверженные повторному входу, содержат `CommandId`; связанные
  операции и events содержат `CorrelationId`. Повтор завершённого `CommandId`
  возвращает прежний result и не повторяет mutation.
- Idempotency key ограничен gameplay-сессией и включает session, handler/command
  type и `CommandId`. Завершённый result хранится до конца gameplay-сессии,
  поэтому callback внутри сессии не сможет повторить mutation после eviction.

### Количества и деньги

- Item quantity — целочисленное значение: хранимые counts не меньше нуля, а
  command inputs, produced amounts и consumed amounts больше нуля.
- Отсутствующий item имеет quantity zero. Отрицательные quantities и
  floating-point item counts недопустимы; signed deltas разрешены только как
  факты в events.
- Batch operations нормализуют повторяющиеся item IDs, проверяют arithmetic
  overflow, валидируют все inputs и фиксируют либо все изменения, либо ни одного.
- Currency — отдельное неотрицательное целое значение в минимальных денежных
  единицах. Это не item quantity, и изменять его может только Economy Service.
- Growth progress в диапазоне `[0, 1]`, elapsed time, duration и modifiers
  используют отдельные value types и никогда не представляются item quantities.
- Физическая representation обычно представляет один item, если definition явно
  не разрешает stack representation. Источником истины остаётся Inventory или
  Delivery Queue entry, а не transform или scene object.

### Команды, запросы и события

**Commands** выражают намерение в повелительной форме и адресованы одному
публичному application API. У каждой command ровно один handler; она возвращает
типизированный success или rejection и при отказе ничего не изменяет. Commands,
которые могут дважды возникнуть из VR input или callbacks, используют
`CommandId`; устаревшие физические callbacks также могут передавать ожидаемую
версию aggregate. Выполнение commands сериализовано внутри gameplay-сессии.

**Queries** доступны только для чтения и возвращают неизменяемые snapshots или
read models. Они не раскрывают aggregates или mutable collections. Query никогда
не резервирует, не расходует и не продвигает состояние.

**Events** — неизменяемые факты в прошедшем времени, публикуемые только после
успешного commit. Event указывает owner/entity, aggregate version или sequence,
reason, correlation/command ID и before/after value либо delta, необходимые
наблюдателям. Consumers устойчивы к replay и duplication. Events обновляют
projections или планируют более позднюю явную command, но не дают обходного пути
для вложенной mutation владельца.

Примеры events: `InventoryChanged`, `PlantStateChanged`, `WaterRequested`,
`PlantReady`, `CraftCompleted`, `OrderAccepted`, `OrderCompleted`,
`BalanceChanged`, `PurchaseCompleted` и `DeliveryEntryChanged`. Имена остаются
концептуальными до выбора языка реализации и naming convention.

### Публичные межсистемные контракты

Это концептуальные границы возможностей, а не предписанные имена interfaces:

| Возможность | Команды | Запросы/события |
| --- | --- | --- |
| Каталоги | Валидация при старте | Разрешение item, plant, recipe, order-template и shop-offer definitions по стабильному ID |
| Инвентарь | Атомарные transfer, add, consume и transform | Snapshot quantity/capacity; `InventoryChanged` |
| Растения | Plant, advance time, water, apply fertilizer, harvest/reset через coordinator | Snapshot слота; state, water и readiness events |
| Крафт | Однократный craft recipe | Доступные recipes, requirements, типизированный result; `CraftCompleted` |
| Заказы | Generate, accept, complete или cancel по lifecycle | Snapshots доступного/активного заказа и readiness; lifecycle events |
| Экономика | `TrySpend` и `Credit` с причиной и command identity | Snapshot balance; `BalanceChanged` |
| Магазин | Однократная покупка offer | Offer catalog и типизированный purchase result |
| Доставка | Enqueue, mark available после materialization и однократный claim | Упорядоченный snapshot queue; `DeliveryEntryChanged` |
| Индикаторы | Show, rebind, reconcile и hide визуальных projections | Потребляет domain queries/events; не создаёт gameplay mutations |

Runtime-neutral ports включают injected time source, random source, domain event
sink и session transaction boundary. Их конкретные реализации откладываются до
выбора движка и execution model.

### Атомарные процессы с несколькими владельцами

Именованные handlers ниже предварительно валидируют каждого участника, выполняют
commit через session transaction/unit of work (либо доказуемо безошибочный commit
в serialized dispatcher) и публикуют events только после успеха всей операции.

| Обработчик | Координируемые владельцы и правило фиксации |
| --- | --- |
| Посадка (`Plant Seed`) | Inventory расходует один seed, а Planting Slot переходит `Empty -> Growing`, либо не меняется ничего |
| Применение удобрения (`Apply Fertilizer`) | Planting Slot принимает один modifier, а Inventory расходует одно удобрение, либо не меняется ничего |
| Сбор (`Harvest`) | Inventory принимает настроенный output до сброса Planting Slot, иначе готовое растение сохраняется |
| Крафт (`Craft Recipe`) | Inventory атомарно преобразует все recipe inputs в настроенные outputs |
| Сдача заказа (`Deliver Order`) | Inventory расходует точное medicine, Order завершается один раз, Economy начисляет reward один раз, либо не меняется ничего |
| Покупка (`Purchase`) | Economy списывает точную цену, а Delivery Queue добавляет точный product, либо не меняется ничего |
| Получение доставки (`Claim Delivery`) | Inventory принимает item, а Delivery Queue отмечает entry как claimed, иначе entry остаётся доступной для повтора |

Запрещены общий `GameManager`, глобальный mutable service locator и aggregate,
объединяющий все системы. Новый cross-system flow получает узкий именованный
handler, а не превращает существующий coordinator в God Object.

## Проверка владельцев по сценариям

Ниже разобраны сценарии из issues, чтобы подтвердить единственного владельца
каждого изменения состояния.

### Сценарий A: от саженца до собранного растения ([#32](https://github.com/Techno-Cobras/vr-game/issues/32))

1. Adapter склада саженцев запрашивает transfer между Inventory контейнера и
   игрока только после успешной передачи; ошибка spawn ничего не теряет.
2. Посадка атомарно расходует один seed в Inventory и переводит целевой Planting
   Slot из `Empty` в `Growing`. Занятый slot не изменяет ни одну систему.
3. Только Planting Slot продвигает progress, выбирает и хранит water threshold,
   входит в `NeedsWater`, применяет один fertilizer modifier, достигает
   `ReadyToHarvest` и продолжает рост после валидного полива.
4. Индикаторы воды и готовности только проецируют snapshot/events слота.
5. Сбор атомарно добавляет настроенный output в Inventory и затем сбрасывает
   Planting Slot. Ошибка add сохраняет готовое растение.

### Сценарий B: от растений до лекарства ([#33](https://github.com/Techno-Cobras/vr-game/issues/33))

1. Crafting Table читает Recipe Catalog и snapshot Inventory, затем отправляет
   одну craft command с `CommandId`, `RecipeId` и `InventoryId`.
2. Crafting Service находит неизменяемое определение рецепта и запрашивает у
   владельца Inventory один атомарный transform inputs в outputs.
3. Нехватка ingredients, недостаточная output capacity или дубликат command
   оставляют quantities без изменений. UI наблюдает только типизированный
   result/event.

### Сценарий C: от принятия заказа до оплаты ([#34](https://github.com/Techno-Cobras/vr-game/issues/34))

1. Order Board отправляет `AcceptOrder`; только Order Service меняет active order
   и lifecycle. Waypoint проецирует это состояние.
2. Неправильная или неполная доставка отклоняется без mutation.
3. Сдача заказа атомарно расходует точное medicine в Inventory, один раз
   завершает Order и один раз начисляет reward через Economy Service.
4. Повторная доставка возвращает прежний/already-completed result. Waypoint
   скрывается в ответ на committed lifecycle, а не изменяет заказ.

### Сценарий D: от покупки на планшете до использования ([#35](https://github.com/Techno-Cobras/vr-game/issues/35))

1. Tablet UI запрашивает Shop Catalog и Economy, затем отправляет одну purchase
   command.
2. Покупка атомарно списывает деньги через Economy Service и добавляет запись
   через Delivery Queue. Некорректный product, недостаток средств или дубликат
   command не приводят к частичному списанию или второй entry.
3. Delivery Box материализует pending entries, но Delivery Queue остаётся
   источником истины при недостатке capacity или ошибке spawn.
4. Получение доставки переносит точный item в Inventory и отмечает queue entry
   как `Claimed` только после успешной передачи; иначе она остаётся retryable.
5. Последующая посадка или применение удобрения расходует ресурс через
   соответствующий именованный handler, а не через physical representation.

### Сценарий E: полный повторяемый gameplay loop ([#36](https://github.com/Techno-Cobras/vr-game/issues/36))

Сценарии A, B, C и D объединяются в одной сессии. На каждой границе проверяются
точные Inventory quantities, один lifecycle Planting Slot, один
active/completed Order, точная Economy delta и один enqueue/claim Delivery Queue.
Второй цикл выращивания начинается с купленного ресурса без прямой подмены
состояния. Сценарий E — проверка владельцев существующих handlers, а не новый
координирующий сервис.

## Последствия

- Gameplay state можно тестировать без VR- или engine objects.
- Параллельная разработка подсистем может опираться на стабильных владельцев и
  transaction seams.
- UI, индикаторы и физические объекты можно уничтожать и восстанавливать без
  потери авторитетного состояния.
- Cross-owner workflows требуют явного проектирования transaction и idempotency.
  Это добавляет контракты, но предотвращает partial state и дубли наград/items.
- Будущая реализация должна добавить dependency checks, подтверждающие отсутствие
  engine/UI/VR references в Domain и невозможность записи presentation в state stores.

## Отложенные решения

Движок и версия, XR runtime/framework, input phases, physics/lifecycle APIs,
threading, concrete serialization, persistence/save migration, реализация
transactions, целевой шлем и frame-time budgets намеренно не определены. Будущие
ADR могут сопоставить эти границы выбранному stack, но изменение владения
состоянием требует нового ADR, который заменит этот.

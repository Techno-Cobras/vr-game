# Надёжная очередь доставки (VR-30)

`PendingDeliveryLedger` — авторитетная очередь текущей сессии, общая с
`PurchaseService`. Immutable snapshots сохраняют исходный порядок покупок и
историю записей в состоянии `Claimed`. `TryGetNextPending` возвращает самую
раннюю запись `Pending`; одинаковые покупки получают разные ID. Событие `Added`
сохраняет совместимость с наблюдателями покупок, а `Changed` публикует добавления
и переходы lifecycle, изолируя исключения отдельных подписчиков.

Контракт адаптера Delivery Box (VR-31): сначала зарезервировать свободный slot и
подготовить representation, затем вызвать `MarkAvailable` только после успешной
материализации. Недостаток capacity или ошибка staging оставляют запись
`Pending`, а адаптер откатывает временный representation. Повторный
`MarkAvailable` для `Available` идемпотентен; `Claimed` не возвращается в
`Available`. Владение физическими объектами, slots и rollback принадлежат
адаптеру, а не DOMAIN.

Перед выдачей игроку адаптер проверяет и подготавливает handoff, не освобождая
representation, затем вызывает `Claim` с ID записи, player Inventory и валидным
`InventoryChangeContext`. Inventory и состояние `Claimed` фиксируются до первого
callback. Только успешный `Claim` разрешает завершить handoff и удалить
representation. Ошибка Inventory add оставляет запись `Available`: после
устранения причины точный retry использует тот же context.

Первый валидный claim резервирует payload команды: entry ID, Inventory ID,
reason и correlation. Повтор с другим payload получает `CommandConflict`, даже
если исходная попытка была неуспешной. Точный повтор успешной команды возвращает
replay без второго добавления. FIFO определяет порядок материализации, но не
запрещает игроку выбрать любой доступный физический объект.

`Added` и `Changed` вызываются под блокировкой ledger. Delivery Box не должен
синхронно запускать `MarkAvailable` или `Claim` из callback: re-entry будет
отклонён. Адаптер планирует переход на следующий Unity tick и перечитывает
snapshot после пропущенного уведомления. Все данные существуют только в текущей
сессии; persistence находится вне scope MVP.

`DeliveryQueueTests` покрывают повторные покупки, FIFO, immutable snapshots,
переходы `Pending → Available → Claimed`, retry после ошибки, replay/conflict,
observer failures и атомарный claim. Проверки выполняются на Unity `6000.3.25f1`
командами `scripts/Test-UnitySmoke.ps1` и `scripts/Test-UnityProject.ps1`.

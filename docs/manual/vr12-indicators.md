# Ручная VR-проверка world-space indicators

Проверка выполняется на Meta Quest 3 через Android build или Quest Link. Она не
меняет `Startup.unity`: тестовые objects создаются временно и не сохраняются.

1. Открыть `Startup.unity` и временно добавить prefab
   `WorldSpaceIndicatorPresenter` из `Assets/_Project/Presentation/Indicators/Assets`.
2. Создать два временных target objects, добавить им `IndicatorTarget`, а в
   presenter назначить XR Camera как `viewer`.
3. Запустить Play Mode, выбрать presenter и в custom inspector назначить первый
   `Preview Target`.
4. По очереди нажать кнопки water, ready-to-harvest и delivery. Проверить разные
   подписи/цвета, контраст и billboard на дистанциях 1, 2 и 3 метра.
5. Назначить второй target и повторно показать вариант. Отключение старого target
   не должно скрывать indicator; отключение текущего должно убрать его сразу.
6. Проверить hide/re-show, уничтожение target, pause/resume и потерю/возврат focus.
7. В Profiler подтвердить отсутствие scene searches, per-frame GC allocations,
   physics/input callbacks и удержание целевых 72 FPS.
8. Выйти из Play Mode и удалить временные objects без сохранения сцены.

Автоматические EditMode/PlayMode tests проверяют конфигурацию трёх variants,
reconcile/rebind, cleanup, read-only visuals и billboard с injected viewer.

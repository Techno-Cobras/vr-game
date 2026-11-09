# VR Scenario B: от растений до лекарства (#33)

## Автоматическая часть

`CraftingScenarioBTests.ScenarioB_ConfiguredRecipe_TableToMedicineConservesInventory`
в EditMode проходит обе definitions из сериализованных
`Assets/_Project/Crafting/Samples/RecipeCatalog.asset` и `ItemCatalog.asset`.
Количество ingredients и output берётся из assets, assets не изменяются.
Настоящие Inventory, CraftingService, relays и CraftingTableController связаны
с recording view; XRI, физический ввод и rendering проверяются отдельно.

Тест загружает две порции ingredients, выбирает recipe навигацией стола,
подтверждает крафт и проверяет точные изменения всех catalog items и одну
inventory mutation/event. Повтор того же ввода в одном frame и reentrant callback
не создают второго крафта даже при достаточном остатке ingredients. Replay
исходного domain command не меняет inventory и не повторяет события.
Новая явная навигация позволяет второй крафт; третья попытка при нехватке
ingredients сохраняет все quantities/version/events. View отражает availability,
готовое medicine и missing-ingredients feedback на соответствующих границах.
Отдельный тест `ScenarioB_PartialShortage_PreservesAvailableIngredientsAndReportsExactMissingQuantity`
выбирает configured рецепт с несколькими ingredients: один ресурс доступен,
другого не хватает ровно на единицу. И UI precheck, и domain transaction
сохраняют все quantities/version/events; missing details указывают точный дефицит.

Запуск: `pwsh -NoProfile -File scripts/Test-UnityProject.ps1` из корня проекта
в Unity `6000.3.25f1`. Существующий PlayMode
`CraftingTablePlayModeTests` дополнительно проверяет wiring Startup.

## Ручной проход Quest 3

Запишите commit, APK, дату, устройство, версию runtime и проверяющего.
Используйте Startup, Touch Plus и свежую сессию. До начала зафиксируйте
выбранный recipe ID, его configured ingredients/output, исходные quantities всех
затрагиваемых items. Через существующий harvest flow подготовьте ровно две
порции ingredients для этого recipe; подтвердите количества через авторитетный
Inventory/read models при подключённой отладке. После setup не подменяйте state.

1. Подойдите к Crafting Table, выберите рецепт кнопками навигации. Список,
   название, quantities ingredients, owned/output и availability соответствуют
   recipe data и Inventory. Для multi-ingredient рецепта видны все ingredients.
2. Подтвердите один раз. Получен ровно configured output, каждый ingredient
   уменьшился ровно на required quantity, остальные items не изменились.
   View показывает успех и обновлённые остатки без переоткрытия интерфейса.
3. При повторном проходе шага 2 подайте два callbacks в одном frame через
   instrumented input, сверяясь с автоматическим тестом. Ожидается один output
   и одно списание. Два нажатия в разных frames могут означать два отдельных
   намеренных крафта; не принимайте это за failure duplicate guard.
4. Выполните второй намеренный крафт после отпускания ввода и нового frame.
   Суммарно получено две порции output, ingredients исчерпаны. В UI отображается
   нехватка; попытка подтверждения не меняет ни один item и не создаёт output.
5. Переключите рецепт и вернитесь. Missing details и остатки сохранились.
   Попробуйте рецепт, для которого не хватает хотя бы одного ingredient:
   никакая часть имеющихся ingredients не списывается.
6. Снимите шлем/приостановите приложение. Пока focus отсутствует или pause
   активна, input не создаёт крафт. После возврата видны актуальные quantities.
7. Повторите в свежей сессии для второго configured recipe, включая вариант
   с несколькими ingredients. Запишите expected/actual quantities после каждого
   подтверждения и все defects со steps to reproduce.

## Результаты

- EditMode/PlayMode: НЕ ВЫПОЛНЕНЫ — локально нет Editor `6000.3.25f1`.
- Ручной Quest 3: НЕ ВЫПОЛНЕН; требуется устройство и фактическая сборка.
- Для принятия #33 приложите test XML, APK/commit и результаты шагов с
  quantities до/после. Этот документ описывает ожидаемое поведение, а не
  подтверждает выполнение VR-прохода.

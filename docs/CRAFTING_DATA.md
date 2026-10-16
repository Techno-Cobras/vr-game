# Данные лекарств и рецептов

Лекарства используют общий `ItemDefinition` с категорией `Medicine` и стабильным
`ItemId`. Отдельного идентификатора лекарства нет: Inventory и будущая система
заказов смогут использовать тот же каталог предметов.

`RecipeDefinition` находится в DATA и содержит стабильный `RecipeId` с префиксом
`recipe.`, отображаемое имя, ингредиенты (`ItemId`, положительное целое количество),
ID лекарства и положительное количество результата. Definitions неизменяемы;
входные коллекции копируются. Повтор одного ингредиента считается ошибкой данных,
а не неявным суммированием количества.

`RecipeCatalog` проверяет существование всех предметов в переданном `ItemCatalog`,
категорию `Medicine` у результата и уникальность ID рецептов. Каталог предоставляет
детерминированный список, `TryGet` и `GetRequired` по `RecipeId`. Валидация не
меняет инвентарь и не запускает крафт.

## Авторские assets

1. Создайте лекарство через `Create > VR Game > Items > Definition`, задайте
   категорию `Medicine`, стабильный `item.medicine.*`, representation и stack rules.
2. Добавьте лекарство и все ингредиенты в общий `ItemCatalogAsset`.
3. Создайте `RecipeDefinitionAsset` через меню `Create > VR Game > Crafting`.
   Укажите `recipe.*`, имя, ссылки на ingredient assets, количества и output item.
4. Добавьте рецепты в `RecipeCatalogAsset`. В composition root сначала вызовите
   `ItemCatalogAsset.Build()`, затем `RecipeCatalogAsset.Build(runtimeItems)`.
   Передайте полученный неизменяемый каталог будущему crafting service.

Изменение количеств в Inspector влияет на следующий вызов `Build`, не требует
правки кода и не меняет ранее построенный runtime snapshot. Рецепт не владеет
копией item definitions: ссылки разрешаются по стабильным IDs в общем каталоге.
Неизвестные items, пустые ссылки/коллекции, дубли и неположительные количества
отклоняются при построении данных, включая значения, записанные в обход Inspector.

## Примеры

`Assets/_Project/Crafting/Samples/` содержит отдельный пример общего item catalog,
переиспользующий seed/harvest assets из SeedStorage и добавляющий два лекарства.
Он не подменяет каталог стартовой сцены.

| Recipe ID | Ингредиенты | Результат |
| --- | --- | --- |
| `recipe.basil-tonic` | 2 × `item.harvest.basil` | 1 × `item.medicine.basil-tonic` |
| `recipe.herbal-blend` | 1 × `item.harvest.basil`, 2 × `item.harvest.mint` | 2 × `item.medicine.herbal-blend` |

Оба лекарства используют простой prefab-заглушку. Крафт-транзакция, расход
ингредиентов и VR-интерфейс реализуются в задачах #23 и #24.

## Проверки

`scripts/Test-UnityProject.ps1` запускает EditMode-проверки каталога и
ScriptableObject adapters, включая реальные sample assets, изменение количеств,
невалидные ссылки, дубли и неизменяемость данных. Общая smoke-проверка остаётся
`scripts/Test-UnitySmoke.ps1`.

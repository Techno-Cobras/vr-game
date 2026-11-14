# VR-34 — Scenario C: от принятия заказа до оплаты

## Автоматический сценарий

PlayMode-тест `ScenarioC_FromBoardAcceptanceToSinglePayoutAndReplay` использует
production-конфигурацию `Startup` и проходит один связный поток:

1. На Order Board выбирается детерминированный заказ на две единицы лекарства
   с наградой 60 и принимается через `OrderBoardController`.
2. Проверяются единственный active order, видимый delivery waypoint и отказ
   принять второй заказ.
3. Wrong medicine и partial quantity отправляются в общий
   `OrderDeliveryCoordinator`. После каждого отказа Inventory, Orders, Economy,
   balance, ledger и waypoint должны остаться без изменений.
4. Точное количество лекарства передаётся через VR delivery controller.
   Транзакция атомарно списывает две единицы, завершает заказ, начисляет одну
   награду и скрывает waypoint.
5. Повтор того же VR-intent возвращает replay без второго списания или payout.
   Новый intent после завершения получает `NoActiveOrder` и также ничего не
   изменяет.

## Ручная проверка на Quest 3

1. Запустить `Assets/_Project/Scenes/Startup.unity` на поддерживаемом runtime.
2. На Order Board выбрать заказ «Травяная смесь» (quantity 2, reward 60) и
   нажать `ПРИНЯТЬ`.
3. Убедиться, что board показывает active order, а над delivery point появился
   waypoint. Попытка принять второй заказ должна быть отклонена с понятным
   русским статусом.
4. Попытаться доставить неправильное лекарство и затем только одну единицу
   нужного лекарства. Заказ, inventory, баланс 100 и waypoint не должны
   измениться; rejection feedback должен отличаться от success feedback.
5. Подготовить ровно две единицы нужного лекарства и активировать delivery
   point контроллером. Обе единицы должны списаться, заказ — перейти в
   `Completed`, баланс — стать 160, waypoint — исчезнуть.
6. Не завершая тот же select-intent, повторить callback: награда и списание не
   должны повториться. После отпускания и нового взаимодействия точка должна
   сообщить, что активного заказа нет.
7. Во время active order снять focus или отправить приложение в pause.
   Устаревший intent не должен выполнить доставку после возврата.

Аппаратный grab, haptic feedback и читаемость marker проверяются вручную;
атомарность, replay и lifecycle marker покрываются PlayMode-тестом.

## Команды проверки

```powershell
pwsh -NoProfile -File scripts/Test-RepositoryPolicy.ps1
pwsh -NoProfile -File scripts/Test-UnitySmoke.ps1
pwsh -NoProfile -File scripts/Test-UnityProject.ps1
pwsh -NoProfile -File scripts/Build-Android.ps1
```

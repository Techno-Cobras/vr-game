# Unity CI

GitHub Actions запускает `.github/workflows/unity-ci.yml` при push в `main`
и `codex/**`, для pull request в `main` и вручную через Actions → Unity CI.
Версия Editor читается из `ProjectSettings/ProjectVersion.txt` (`6000.3.25f1`).
Локальная установка Unity для запуска CI не нужна.

## Настройка активации

Администратор добавляет secrets в Settings → Secrets and variables → Actions:

- Personal: `UNITY_LICENSE` (содержимое активированного `.ulf`), `UNITY_EMAIL`,
  `UNITY_PASSWORD`.
- Pro: `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
- Floating license: вместо перечисленного — `UNITY_LICENSING_SERVER`, URL
  доступного из GitHub-hosted runner сервера лицензий.

Используйте [официальную инструкцию GameCI по активации](https://game.ci/docs/github/activation/)
для своего типа лицензии. Secrets вводятся только в GitHub; не отправляйте их
в чат и не сохраняйте в исходниках или логах. Workflow проверяет наличие
настроек и завершается с ошибкой, если активация не настроена. Отсутствующая
лицензия не превращается в успешную или пропущенную Unity-проверку.
Fork PR не получает repository secrets и требует отдельного доверенного запуска
проверяемого commit в ветке репозитория; `pull_request_target` не используется.

## Что проверяется

1. На Windows выполняется `scripts/Test-RepositoryPolicy.ps1`.
2. На Ubuntu GameCI выполняет EditMode и PlayMode tests последовательно.
   Это эквивалент автоматической части `scripts/Test-UnityProject.ps1`;
   coverage instrumentation отключена, поскольку проект не содержит coverage package.
3. После успешных тестов запускается существующий
   `VrGame.Editor.BuildAutomation.BuildAndroid`. Он сначала выполняет `Smoke`
   (ту же проверку, что `scripts/Test-UnitySmoke.ps1`), затем собирает Android APK.
   Gameplay, project settings и версии пакетов workflow не изменяет.

Unity-запуски внутри одного workflow не параллелятся. Одновременные запуски
разных веток могут потребовать дополнительных license seats. Новый запуск той
же Git ref отменяет устаревший. В Actions доступны статус jobs и artifact
`unity-results-<run>-<attempt>` с имеющимися test XML, логами и APK; artifact
сохраняется 14 дней и загружается также после ошибки. Если Unity не запустился,
результатов тестов и APK может не быть.

## Приёмка и слияние

Зелёные Repository policy и Unity tests and Android smoke build относятся
к commit, который проверялся Actions. После изменения ветки дождитесь нового
прогона. При необходимости администратор может назначить эти jobs обязательными
в branch protection. Ручные проверки tracking, haptic и удобства взаимодействий
на физическом Quest 3 остаются отдельной частью приёмки: hosted CI их не выполняет.

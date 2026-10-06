# Проверка VR project skeleton (#8)

Дата проверки: 2026-10-06.

## Проверенная конфигурация

- Unity Editor: `6000.3.25f1` (`e1dba0a9aba4`)
- Windows batchmode host, Unity Personal license
- Android Build Support
- OpenJDK `17.0.18+8`
- Android NDK `27.2.12479018`
- Android Platform API 34, ARM64, IL2CPP, Vulkan
- Input System `1.20.1`
- URP `17.3.0`
- XRI `3.6.1`
- XR Management `4.7.0`
- OpenXR `1.18.0`

## Команды и результат

```powershell
pwsh -NoProfile -File scripts/Test-RepositoryPolicy.ps1
pwsh -NoProfile -File scripts/Test-UnitySmoke.ps1
pwsh -NoProfile -File scripts/Test-UnityProject.ps1
pwsh -NoProfile -File scripts/Build-Android.ps1
```

Результат: repository policy прошла; smoke marker `VR_GAME_SMOKE_OK`; EditMode
`3/3`; PlayMode `3/3`; development APK успешно создан с marker
`VR_GAME_ANDROID_BUILD_OK`. APK находится в игнорируемом каталоге `Builds/` и не
входит в Git.

## Покрытие smoke

- пакетные зависимости разрешаются без ошибок;
- startup scene открывается и содержит ровно один XR Origin;
- XR Interaction Manager, controller-first rig и locomotion присутствуют;
- основной Input Action Asset содержит action maps и bindings;
- в сцене есть XR Grab Interactable и интерактивный world-space UI;
- единственная startup scene включена в Build Settings;
- Android arm64/IL2CPP/Vulkan build завершается успешно.

## Ограничение

Физический Quest 3 в этой среде не подключён. Установка APK, controller tracking,
haptics, cold start, suspend/resume и подтверждение стабильных 72 FPS остаются
обязательной hardware-проверкой перед выпуском, но не входят в acceptance criteria
задачи #8.

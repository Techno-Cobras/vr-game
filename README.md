# VR Game

Минимальный VR-каркас на Unity 6.3 LTS для Meta Quest 3 standalone.

## Быстрый старт

1. Установите Unity `6000.3.25f1` с Android Build Support, OpenJDK, SDK и NDK.
2. Откройте корень репозитория как Unity project.
3. Запустите стартовую сцену `Assets/_Project/Scenes/Startup.unity`.

Скрипты находят Editor требуемой версии через Unity Hub. Для нестандартной
установки передайте `-UnityEditor 'C:\path\to\Unity.exe'` или задайте переменную
окружения `UNITY_EDITOR`.

Проверки из корня репозитория:

```powershell
pwsh -NoProfile -File scripts/Test-RepositoryPolicy.ps1
pwsh -NoProfile -File scripts/Test-UnitySmoke.ps1
pwsh -NoProfile -File scripts/Test-UnityProject.ps1
pwsh -NoProfile -File scripts/Build-Android.ps1
```

Подробности структуры, зависимостей и проверок описаны в
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

# Project Architecture

## Current state

This repository currently contains only a placeholder `README.md` and the Codex collaboration configuration. No game project, source code, scenes/maps, assets, dependency manifests, build scripts, tests, or continuous-integration workflows are committed yet.

## Engine and version

- Выбранный движок: Unity 6.3 LTS `6000.3.25f1`.
- Обязательная платформа MVP: Meta Quest 3 standalone, Android arm64, IL2CPP,
  Vulkan, 72 Гц.
- XR: Unity OpenXR Plugin `1.18.0` поверх системного Horizon OS OpenXR runtime.
- Ввод: Unity Input System `1.20.1`.
- Взаимодействия: XR Interaction Toolkit `3.6.1`.
- Управление XR lifecycle: XR Plug-in Management `4.7.0`.
- Render pipeline: URP; точную разрешённую Editor-ом версию зафиксирует package
  manifest при создании проекта.

Выбор и ограничения зафиксированы в
[ADR 0005](adr/0005-vr-technology-stack.md). Сам Unity project и dependency
manifest ещё не добавлены; до задачи #8 указанные пакеты являются принятым
baseline, а не установленными файлами репозитория.

## Repository structure

- `.gitignore`: правила исключения локальных кэшей, сборок и файлов IDE.
- `.gitattributes`: LF для текста и Git LFS для двоичных Unity-ассетов.
- `.codex/config.toml`: project-level Codex multi-agent settings.
- `.codex/agents/`: project-scoped specialist agent definitions.
- `AGENTS.md`: shared development, delegation, Git, review, and merge contract.
- `docs/ARCHITECTURE.md`: factual project map; expand it as systems are committed.
- `docs/REPOSITORY_POLICY.md`: правила metadata, LFS, владения файлами и
  разрешения конфликтов.
- `scripts/Test-RepositoryPolicy.ps1`: исполняемая проверка `.gitignore` и
  `.gitattributes` без создания fixture-файлов.
- `README.md`: current project placeholder.

## Политика репозитория

Правила хранения Unity-проекта зафиксированы в
[политике репозитория](REPOSITORY_POLICY.md). Авторские файлы `Assets/` вместе с
`*.meta`, `Packages/` и `ProjectSettings/` должны храниться в Git. Локальные
кэши и сборки игнорируются, а двоичные ассеты перечисленных форматов проходят
через Git LFS. Сцены, prefab, общие serialized assets и настройки изменяются
назначенным владельцем; конфликты разрешаются семантически.

## Systems and startup flow

No game systems, scenes/maps, startup flow, player architecture, interaction system, input configuration, or gameplay state implementation are present yet.

Независимые от движка границы доменов MVP определены в
[ADR 0007](adr/0007-mvp-domain-boundaries.md). DATA предоставляет неизменяемые
валидированные definitions; DOMAIN / GAME LOGIC владеет всем авторитетным
runtime-состоянием; VR INTERACTION и UI / PRESENTATION преобразуют намерения и
наблюдают queries/events. Внешние слои могут вызывать публичные application
contracts домена, но Domain никогда не зависит от типов движка, VR, scene или UI.

У каждого изменяемого значения есть один владелец. Cross-system изменения
выполняются узкими именованными атомарными handlers; физические объекты и
presenters никогда не становятся альтернативными хранилищами. Конкретное
сопоставление с движком остаётся неопределённым до добавления движка и project
skeleton.

## Dependencies

No dependency manifest is present.

## Build, test, and lint

Unity build, PlayMode/EditMode tests, lint и static analysis пока недоступны:
project skeleton ещё не добавлен. Политика репозитория проверяется командой:

```powershell
pwsh -NoProfile -File scripts/Test-RepositoryPolicy.ps1
```

После добавления project skeleton здесь должны быть записаны только проверенные
команды, синхронизированные с CI.

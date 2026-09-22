# Project Architecture

## Current state

This repository currently contains only a placeholder `README.md` and the Codex collaboration configuration. No game project, source code, scenes/maps, assets, dependency manifests, build scripts, tests, or continuous-integration workflows are committed yet.

## Engine and version

- Engine: not yet established in repository files.
- Engine version: not available.
- VR/XR framework: not available.

Do not infer Unity, Unreal Engine, Godot, or any XR package until the corresponding project and dependency files are committed.

## Repository structure

- `.codex/config.toml`: project-level Codex multi-agent settings.
- `.codex/agents/`: project-scoped specialist agent definitions.
- `AGENTS.md`: shared development, delegation, Git, review, and merge contract.
- `docs/ARCHITECTURE.md`: factual project map; expand it as systems are committed.
- `README.md`: current project placeholder.

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

No build, test, lint, or static-analysis commands are defined in the repository. Once the project skeleton is committed, record only verified commands here and keep them synchronized with CI.

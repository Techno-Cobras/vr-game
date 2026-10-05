$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

function Assert-Ignored {
    param([Parameter(Mandatory)][string]$Path)

    & git check-ignore --no-index --quiet -- $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Ожидалось, что путь игнорируется: $Path"
    }
}

function Assert-NotIgnored {
    param([Parameter(Mandatory)][string]$Path)

    & git check-ignore --no-index --quiet -- $Path
    if ($LASTEXITCODE -eq 0) {
        throw "Путь не должен игнорироваться: $Path"
    }
}

function Get-Attributes {
    param([Parameter(Mandatory)][string]$Path)

    return (& git check-attr filter diff merge text eol -- $Path) -join "`n"
}

function Assert-TextWithLf {
    param([Parameter(Mandatory)][string]$Path)

    $attributes = Get-Attributes -Path $Path
    if ($attributes -notmatch ': text: set' -or $attributes -notmatch ': eol: lf') {
        throw "Для текстового файла должны быть заданы text и eol=lf: $Path`n$attributes"
    }
}

function Assert-Lfs {
    param([Parameter(Mandatory)][string]$Path)

    $attributes = Get-Attributes -Path $Path
    foreach ($attribute in @('filter: lfs', 'diff: lfs', 'merge: lfs', 'text: unset')) {
        if ($attributes -notmatch [regex]::Escape(": $attribute")) {
            throw "Для двоичного файла не найден атрибут $attribute`: $Path`n$attributes"
        }
    }
}

Push-Location $repoRoot
try {
    foreach ($path in @(
        'Library/ArtifactDB',
        'Temp/Test.tmp',
        'Obj/cache.bin',
        'Build/game.apk',
        'Builds/game.aab',
        'Logs/Editor.log',
        'UserSettings/EditorUserSettings.asset',
        'MemoryCaptures/capture.snap',
        'Recordings/test.mp4',
        'UIElementsSchema/Schema.xsd',
        '.vs/config.json',
        'Game.sln'
    )) {
        Assert-Ignored -Path $path
    }

    foreach ($path in @(
        'Assets/Scenes/Main.unity',
        'Assets/Scenes/Main.unity.meta',
        'ProjectSettings/EditorSettings.asset',
        'Packages/manifest.json',
        'Packages/packages-lock.json'
    )) {
        Assert-NotIgnored -Path $path
    }

    foreach ($path in @(
        'Assets/Scenes/Main.unity',
        'Assets/Prefabs/Tool.prefab',
        'Assets/Scenes/Main.unity.meta',
        'Packages/manifest.json',
        'Assets/Input/Controls.inputactions'
    )) {
        Assert-TextWithLf -Path $path
    }

    foreach ($path in @(
        'Assets/Art/Test.PNG',
        'Assets/Audio/Test.WAV',
        'Assets/Models/Test.FBX',
        'Assets/Video/Test.MP4',
        'Assets/Fonts/Test.TTF',
        'Assets/Plugins/Test.DLL',
        'Assets/Plugins/Test.bundle/Contents/MacOS/Test'
    )) {
        Assert-Lfs -Path $path
    }

    & git lfs version *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Git LFS не установлен или недоступен.'
    }

    Write-Host 'Политика репозитория проверена успешно.' -ForegroundColor Green
}
finally {
    Pop-Location
}

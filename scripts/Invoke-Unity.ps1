[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$UnityArguments,

    [string]$UnityEditor
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectVersion = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Raw
$version = [regex]::Match($projectVersion, 'm_EditorVersion:\s*(\S+)').Groups[1].Value

$candidates = [System.Collections.Generic.List[string]]::new()
if (-not [string]::IsNullOrWhiteSpace($UnityEditor)) {
    $candidates.Add($UnityEditor)
}
if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR)) {
    $candidates.Add($env:UNITY_EDITOR)
}

$hubRoots = [System.Collections.Generic.List[string]]::new()
$secondaryInstallPath = Join-Path $env:APPDATA 'UnityHub/secondaryInstallPath.json'
if (Test-Path -LiteralPath $secondaryInstallPath) {
    $hubRoots.Add((Get-Content -LiteralPath $secondaryInstallPath -Raw | ConvertFrom-Json))
}
if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $hubRoots.Add((Join-Path $env:ProgramFiles 'Unity/Hub/Editor'))
}
if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
    $hubRoots.Add((Join-Path ${env:ProgramFiles(x86)} 'Unity/Hub/Editor'))
}

foreach ($hubRoot in $hubRoots) {
    $candidates.Add((Join-Path $hubRoot "$version/Editor/Unity.exe"))
}

$editor = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($editor)) {
    throw "Unity Editor $version не найден. Передайте -UnityEditor или задайте UNITY_EDITOR. Проверено: $($candidates -join ', ')"
}

$arguments = @('-batchmode', '-nographics', '-projectPath', $projectRoot) + $UnityArguments
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $editor
$startInfo.UseShellExecute = $false
foreach ($argument in $arguments) {
    [void]$startInfo.ArgumentList.Add($argument)
}

$process = [System.Diagnostics.Process]::Start($startInfo)
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    throw "Unity завершился с кодом $($process.ExitCode)."
}

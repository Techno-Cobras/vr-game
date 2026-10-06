[CmdletBinding()]
param(
    [string]$UnityEditor
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Invoke-Unity.ps1') -UnityEditor $UnityEditor -UnityArguments @(
    '-quit', '-executeMethod', 'VrGame.Editor.BuildAutomation.BuildAndroid',
    '-logFile', (Join-Path $projectRoot 'Logs/android-build.log')
)

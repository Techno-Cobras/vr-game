[CmdletBinding()]
param(
    [string]$UnityEditor
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Invoke-Unity.ps1') -UnityEditor $UnityEditor -UnityArguments @(
    '-quit', '-executeMethod', 'VrGame.Editor.BuildAutomation.Smoke',
    '-logFile', (Join-Path $projectRoot 'Logs/smoke.log')
)

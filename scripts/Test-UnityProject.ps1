[CmdletBinding()]
param(
    [string]$UnityEditor
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$results = Join-Path $projectRoot 'Logs/TestResults'
New-Item -ItemType Directory -Path $results -Force | Out-Null

& (Join-Path $PSScriptRoot 'Invoke-Unity.ps1') -UnityEditor $UnityEditor -UnityArguments @(
    '-runTests', '-testPlatform', 'EditMode',
    '-testResults', (Join-Path $results 'editmode.xml'),
    '-logFile', (Join-Path $projectRoot 'Logs/editmode-tests.log')
)

& (Join-Path $PSScriptRoot 'Invoke-Unity.ps1') -UnityEditor $UnityEditor -UnityArguments @(
    '-runTests', '-testPlatform', 'PlayMode',
    '-testResults', (Join-Path $results 'playmode.xml'),
    '-logFile', (Join-Path $projectRoot 'Logs/playmode-tests.log')
)

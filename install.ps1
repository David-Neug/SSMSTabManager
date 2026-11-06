#Requires -RunAsAdministrator
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$ide = 'C:\Program Files (x86)\Microsoft SQL Server Management Studio 19\Common7\IDE'
$target = Join-Path $ide 'Extensions\SSMSTabManager'
$projectDir = Join-Path $PSScriptRoot 'src'
$source = Join-Path $projectDir 'bin\Release'

if (Get-Process ssms -ErrorAction SilentlyContinue) {
    Write-Warning 'SSMS běží a má načtenou DLL rozšíření. Zavři všechny instance SSMS a spusť skript znovu.'
    exit 1
}

if ($Uninstall) {
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Write-Host "Odinstalováno: $target"
} else {
    New-Item -ItemType Directory -Force $target | Out-Null
    'SSMSTabManager.dll', 'SSMSTabManager.pkgdef', 'extension.vsixmanifest' |
        ForEach-Object { Copy-Item (Join-Path $source $_) $target -Force }
    Copy-Item (Join-Path $projectDir 'SSMSTabManager.json') $target -Force
    Write-Host "Nainstalováno: $target"
}

$marker = Join-Path $ide 'Extensions\extensions.configurationchanged'
if (Test-Path $marker) { (Get-Item $marker).LastWriteTime = Get-Date } else { New-Item -ItemType File $marker | Out-Null }

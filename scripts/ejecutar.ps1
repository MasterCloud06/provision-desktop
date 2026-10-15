# Abre la aplicación de escritorio.
#   .\scripts\ejecutar.ps1
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet run --project src/Provision.Desktop

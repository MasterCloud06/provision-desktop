# Compila toda la solución.
#   .\scripts\compilar.ps1            (Debug)
#   .\scripts\compilar.ps1 Release
param([string]$Configuracion = 'Debug')
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

dotnet build Provision.sln -c $Configuracion
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nLa compilación falló. Copia los errores (líneas con 'error') y pásamelos." -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "`nCompilación correcta." -ForegroundColor Green

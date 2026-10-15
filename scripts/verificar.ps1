# Revisa que tengas todo lo necesario para compilar Provisión.
#   .\scripts\verificar.ps1
$ErrorActionPreference = 'Stop'

Write-Host "Revisando .NET SDK..." -ForegroundColor Cyan
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "No se encontró 'dotnet'. Instala el SDK de .NET 10:" -ForegroundColor Red
    Write-Host "  winget install Microsoft.DotNet.SDK.10"
    exit 1
}
$sdks = dotnet --list-sdks
if (-not ($sdks -match '^10\.')) {
    Write-Host "Tienes .NET, pero no la versión 10. SDKs instalados:" -ForegroundColor Yellow
    $sdks
    Write-Host "Instala el 10 con: winget install Microsoft.DotNet.SDK.10"
    exit 1
}
Write-Host "  .NET 10 OK" -ForegroundColor Green
dotnet --version

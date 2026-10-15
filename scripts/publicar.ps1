# Genera Provision.exe listo para instalar en otra PC (no necesita .NET instalado).
#   .\scripts\publicar.ps1
# Resultado: .\publicado\win-x64\Provision.exe
param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$salida = Join-Path 'publicado' $Runtime
dotnet publish src/Provision.Desktop -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $salida
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "`nListo: $salida\Provision.exe" -ForegroundColor Green

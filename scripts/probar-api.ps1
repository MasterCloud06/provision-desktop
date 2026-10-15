# Prueba la conexión con la API recorriendo el flujo completo (login, inventario, caja, venta, finanzas).
# OJO: crea datos de prueba. Úsalo contra tu entorno LOCAL.
#   .\scripts\probar-api.ps1 -Email admin@provision.local -Password CambiaEsto123
param(
    [string]$Url = 'http://localhost:3000/api/v1',
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet run --project tools/Provision.Core.Pruebas -- $Url $Email $Password

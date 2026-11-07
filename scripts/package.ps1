[CmdletBinding()]
param([switch]$WithoutMedia)
# Packages exclude personal media; shortcut integration is an explicit manual operation.
& (Join-Path $PSScriptRoot 'package-preview.ps1')

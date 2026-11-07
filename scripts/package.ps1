[CmdletBinding()]
param([switch]$WithoutMedia)
# Compatibility entry point: phase-one packages exclude personal media and integration tools.
& (Join-Path $PSScriptRoot 'package-preview.ps1')

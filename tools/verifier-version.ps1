#Requires -Version 7.0
<#
.SYNOPSIS
    Vérifie que la version du dépôt peut être publiée : étiquette = version = section datée du CHANGELOG.

.DESCRIPTION
    Lit la version dans Directory.Build.props, cherche sa section « ## [version] - AAAA-MM-JJ » dans le CHANGELOG
    et, si une étiquette est donnée, vérifie qu'elle vaut « v » suivi de la version. Le résultat est la version.

.PARAMETER Etiquette
    L'étiquette à comparer (v1.0.0) ; sans elle, seules la version et le CHANGELOG sont comparés.
#>
[CmdletBinding()]
param(
    [string] $Etiquette
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent $PSScriptRoot
[xml] $proprietes = Get-Content -Raw (Join-Path $racine 'Directory.Build.props')
$version = @($proprietes.Project.PropertyGroup | ForEach-Object { $_.SelectSingleNode('Version') } | Where-Object { $_ })[0].InnerText.Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "Version « $version » non conforme à SemVer." }

$section = Select-String -Path (Join-Path $racine 'CHANGELOG.md') -Pattern ('^## \[' + [regex]::Escape($version) + '\] - \d{4}-\d{2}-\d{2}\s*$') | Select-Object -First 1
if (-not $section) { throw "Le CHANGELOG n'a pas de section datée pour $version : « ## [$version] - AAAA-MM-JJ »." }
if ($Etiquette -and $Etiquette -cne "v$version") { throw "L'étiquette $Etiquette ne correspond pas à la version $version." }

Write-Host "Version $version : section datée présente$($Etiquette ? ", étiquette $Etiquette conforme" : '')."
$version

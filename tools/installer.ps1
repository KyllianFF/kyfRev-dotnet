#Requires -Version 7.0
<#
.SYNOPSIS
    Construit l'installateur de kyfRev (installer/kyfRev.iss).

.DESCRIPTION
    - lit la version (Directory.Build.props) et, si elle est publiée, sa date dans la section
      datée du CHANGELOG ;
    - publie l'application autonome (runtime .NET inclus, Windows x64), la date de publication
      inscrite dans les métadonnées de l'exécutable (ReleaseDate) ;
    - écrit dans la publication les avis des paquets NuGet publiés (tools/avis-nuget.ps1) ;
    - compile l'installateur avec Inno Setup 6 : artifacts/installer/kyfRev-<version>-installation.exe,
      dont le chemin est le résultat du script.

.PARAMETER Iscc
    Compilateur d'Inno Setup 6 ; par défaut, celui installé pour l'utilisateur, sinon pour tous.

.PARAMETER Version
    Pour les essais seulement : remplace la version du dépôt.

.PARAMETER Signature
    Commande de signature au format d'Inno Setup ($f désigne le fichier à signer) ; sans elle,
    rien n'est signé.
#>
[CmdletBinding()]
param(
    [string] $Iscc,
    [string] $Version,
    [string] $Signature
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent $PSScriptRoot
$sortie = Join-Path $racine 'artifacts\installer'
$application = Join-Path $sortie 'application'

if (-not $Iscc) {
    $Iscc = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $Iscc) { throw 'Inno Setup 6 est introuvable : installez-le, ou désignez ISCC.exe avec -Iscc.' }
}

if (-not $Version) {
    $Version = dotnet msbuild (Join-Path $racine 'src\KyfRev.App\KyfRev.App.csproj') -getProperty:Version
    if ($LASTEXITCODE -ne 0) { throw 'Version du dépôt illisible.' }
    $Version = $Version.Trim()
}
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$') { throw "Version « $Version » non conforme à SemVer." }
$numerique = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"

# Date de publication : « ## [1.0.0-alpha.1] - 2026-11-15 » dans le CHANGELOG.
$section = Select-String -Path (Join-Path $racine 'CHANGELOG.md') -Pattern ('^## \[' + [regex]::Escape($Version) + '\] - (\d{4}-\d{2}-\d{2})$') |
    Select-Object -First 1
$date = $null
$dateTexte = $null
if ($section) {
    $date = $section.Matches[0].Groups[1].Value
    $jour = [DateOnly]::ParseExact($date, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
    $francais = [Globalization.CultureInfo]::GetCultureInfo('fr-FR')
    $dateTexte = ($jour.Day -eq 1 ? '1er' : "$($jour.Day)") + ' ' + $jour.ToString('MMMM yyyy', $francais)
}

Write-Host "kyfRev $Version$($dateTexte ? ", publiée le $dateTexte" : ', non publiée')"

if (Test-Path $application) { Remove-Item -Recurse -Force $application }
$publication = @(
    'publish', (Join-Path $racine 'src\KyfRev.App\KyfRev.App.csproj'),
    '--configuration', 'Release', '--self-contained', '--output', $application,
    "-p:Version=$Version"
)
if ($date) { $publication += "-p:ReleaseDate=$date" }
dotnet @publication | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Publication de l''application impossible.' }
& (Join-Path $PSScriptRoot 'avis-nuget.ps1') -Publication $application

$compilation = @('/Qp', "/O$sortie", "/DVersion=$Version", "/DNumericVersion=$numerique", "/DSource=$application")
if ($dateTexte) { $compilation += "/DReleaseDate=$dateTexte" }
if ($Signature) { $compilation += "/Skyfrev=$Signature", '/DSignTool=kyfrev' }
& $Iscc @compilation (Join-Path $racine 'installer\kyfRev.iss') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Compilation de l''installateur impossible.' }

# Seul résultat du script : le chemin de l'installateur.
Join-Path $sortie "kyfRev-$Version-installation.exe"

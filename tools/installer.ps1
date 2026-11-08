#Requires -Version 7.0
<#
.SYNOPSIS
    Construit l'installateur de kyfRev (installer/kyfRev.iss).

.DESCRIPTION
    - lit la version (Directory.Build.props) et, si elle est publiée, sa date dans la section
      datée du CHANGELOG ;
    - publie l'application autonome (runtime .NET inclus, Windows x64), la date de publication
      inscrite dans les métadonnées de l'exécutable (ReleaseDate) ;
    - y ajoute kyfrev-coffre, l'outil en ligne de commande du coffre ;
    - écrit dans la publication les avis des paquets NuGet publiés (tools/avis-nuget.ps1) ;
    - télécharge le programme d'amorçage de WebView2 et vérifie qu'il est signé par Microsoft ;
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
# libsodium (le chiffrement des coffres, par NSec) charge VCRUNTIME140.dll, que seul le Redistribuable Visual C++ met dans
# Windows ; la publication n'en porte qu'une copie renommée, pour .NET. Sans lui, sur un poste qui ne l'a pas, aucun coffre
# chiffré ne se crée ni ne s'ouvre : il est livré à côté de l'application.
$runtimeVc = Join-Path $env:SystemRoot 'System32\vcruntime140.dll'
if (-not (Test-Path $runtimeVc)) { throw 'vcruntime140.dll est introuvable : installez le Redistribuable Visual C++ sur ce poste.' }
Copy-Item $runtimeVc $application

# kyfrev-coffre, l'outil en ligne de commande du coffre, est livré à côté de l'application.
$outil = @(
    'publish', (Join-Path $racine 'src\KyfRev.Cli\KyfRev.Cli.csproj'),
    '--configuration', 'Release', '--self-contained', '--runtime', 'win-x64', '--output', $application,
    "-p:Version=$Version"
)
dotnet @outil | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Publication de kyfrev-coffre impossible.' }
& (Join-Path $PSScriptRoot 'avis-nuget.ps1') -Publication $application

# Le programme d'amorçage de WebView2 : Microsoft le met à jour à la même adresse, seule sa signature le vérifie.
$amorcage = Join-Path $sortie 'MicrosoftEdgeWebview2Setup.exe'
Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $amorcage -MaximumRetryCount 6 -RetryIntervalSec 20
$signature = Get-AuthenticodeSignature -LiteralPath $amorcage
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw "Le programme d'amorçage de WebView2 n'est pas signé par Microsoft : $($signature.StatusMessage)"
}

$compilation = @('/Qp', "/O$sortie", "/DVersion=$Version", "/DNumericVersion=$numerique", "/DSource=$application", "/DWebView2Setup=$amorcage")
if ($dateTexte) { $compilation += "/DReleaseDate=$dateTexte" }
if ($Signature) { $compilation += "/Skyfrev=$Signature", '/DSignTool=kyfrev' }
& $Iscc @compilation (Join-Path $racine 'installer\kyfRev.iss') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Compilation de l''installateur impossible.' }

# Seul résultat du script : le chemin de l'installateur.
Join-Path $sortie "kyfRev-$Version-installation.exe"

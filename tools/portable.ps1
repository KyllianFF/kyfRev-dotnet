#Requires -Version 7.0
<#
.SYNOPSIS
    Construit la version portable de kyfRev : artifacts/portable/kyfRev-<version>-portable.zip.

.DESCRIPTION
    Part de la publication autonome que tools/installer.ps1 vient d'écrire : le même kyfRev.exe que
    l'installateur. L'archive contient MesRévisions\Application (l'application, portable.json et ses
    licences) et MesRévisions\LISEZMOI.txt ; son empreinte SHA-256 est écrite à côté d'elle. Le coffre
    sera MesRévisions\Coffre. Le chemin de l'archive est le résultat du script.

.PARAMETER Publication
    Le dossier de la publication (artifacts/installer/application).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Publication
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent $PSScriptRoot
$sortie = Join-Path $racine 'artifacts\portable'
$Publication = (Resolve-Path $Publication).Path
$programme = Join-Path $Publication 'kyfRev.exe'
if (-not (Test-Path $programme)) { throw "kyfRev.exe est introuvable dans $Publication : lancez d'abord tools/installer.ps1." }
$version = ((Get-Item $programme).VersionInfo.ProductVersion -split '\+')[0].Trim()

if (Test-Path $sortie) { Remove-Item -Recurse -Force $sortie }
$dossier = Join-Path $sortie 'MesRévisions'
$application = Join-Path $dossier 'Application'
$licences = Join-Path $application 'licences'
New-Item -ItemType Directory -Force $application | Out-Null

# Comme l'installateur : ni symboles, ni dossier Fonts (les polices sont dans les assemblys, leurs licences vont plus bas).
Get-ChildItem $Publication -Force | Where-Object Name -NE 'Fonts' | Copy-Item -Destination $application -Recurse -Force
Get-ChildItem $application -Recurse -Filter *.pdb | Remove-Item -Force

New-Item -ItemType Directory -Force $licences | Out-Null
Copy-Item (Join-Path $racine 'LICENSE') (Join-Path $licences 'LICENSE.txt')
Copy-Item (Join-Path $racine 'src\KyfRev.Rendering\THIRD-PARTY-NOTICES.md') (Join-Path $licences 'THIRD-PARTY-NOTICES-rendu.md')
Copy-Item (Join-Path $racine 'src\KyfRev.Presentation.Wpf\Editing\THIRD-PARTY-NOTICES.md') (Join-Path $licences 'THIRD-PARTY-NOTICES-editeur.md')
Copy-Item (Join-Path $racine 'src\KyfRev.Presentation.Wpf\Schemas\THIRD-PARTY-NOTICES.md') (Join-Path $licences 'THIRD-PARTY-NOTICES-schemas.md')
Copy-Item (Join-Path $racine 'src\KyfRev.DesignSystem\Fonts\OFL-*.txt') $licences

$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $application 'portable.json'), "{ `"coffre`": `"../Coffre`" }`n", $utf8)

$empreinteProgramme = (Get-FileHash (Join-Path $application 'kyfRev.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
$lisezmoi = @"
kyfRev $version : vos révisions selon la méthode des J

DÉMARRER
1. Copiez le dossier « MesRévisions » où vous voulez : Documents, clé USB,
   disque externe…
2. Ouvrez MesRévisions\Application\kyfRev.exe.
3. Au premier lancement, créez le coffre proposé : vos fiches seront rangées
   dans MesRévisions\Coffre.

Rien n'est installé : ni droits administrateur, ni dossier dans votre profil
Windows. Les données de l'application, moteur d'affichage compris, restent dans
MesRévisions\Application, et les sauvegardes dans MesRévisions\Sauvegardes.
Windows et son moteur WebView2 gardent de leur côté quelques traces techniques
(le nom du programme, des réglages) ; aucune ne contient vos fiches.

PRÉREQUIS
Windows 10 ou 11 (64 bits) avec Microsoft Edge WebView2, intégré à Windows 11 et
installé par Windows Update sous Windows 10. S'il manque, kyfRev le dit et
l'écriture s'ouvre en Markdown.

GARDER SES FICHES
Pour tout emporter ou tout sauvegarder, fermez kyfRev puis copiez le dossier
MesRévisions. Vos fiches sont des fichiers Markdown ordinaires (Coffre\fiches),
sauf si vous chiffrez le coffre. Ne modifiez pas Coffre\.suivi à la main.

SI WINDOWS AVERTIT
Cette version n'est pas signée : SmartScreen peut afficher un avertissement.
Comparez d'abord l'empreinte ci-dessous, puis choisissez « Informations
complémentaires » et « Exécuter quand même ».

Empreinte SHA-256 de kyfRev.exe :
$empreinteProgramme
(PowerShell : Get-FileHash .\Application\kyfRev.exe)

UN PROBLÈME ?
MesRévisions\Application\kyfRev.log garde la trace des derniers démarrages.

COMPOSANTS TIERS
Les licences de kyfRev et de ses composants sont dans Application\licences.
"@
[IO.File]::WriteAllText((Join-Path $dossier 'LISEZMOI.txt'), ($lisezmoi -replace "`r?`n", "`r`n"), [Text.UTF8Encoding]::new($true))

$archive = Join-Path $sortie "kyfRev-$version-portable.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($dossier, $archive, [IO.Compression.CompressionLevel]::Optimal, $true, $utf8)
$empreinte = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$archive.sha256", "$empreinte  $(Split-Path -Leaf $archive)`n", $utf8)

Remove-Item -Recurse -Force $dossier
Write-Host "kyfRev $version portable : $archive"

# Seul résultat du script : le chemin de l'archive.
$archive

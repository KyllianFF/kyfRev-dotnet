#Requires -Version 7.0
<#
.SYNOPSIS
    Écrit les avis des paquets NuGet publiés avec kyfRev.

.DESCRIPTION
    Part de ce qui est vraiment publié : les paquets de kyfRev.deps.json, runtime .NET compris, puisque l'application
    est autonome. Reprend de chacun les textes qu'il livre (licence, avis de tiers, licences de polices), et, s'il ne
    livre pas sa licence, le texte rangé dans installer/licences-nuget. Un paquet sans texte de licence arrête le script.

    Résultat : licences\THIRD-PARTY-NOTICES-nuget.md dans la publication, que l'installateur reprend avec le reste.
    Lancé par tools/installer.ps1, après la publication : les paquets doivent être restaurés.

.PARAMETER Publication
    Dossier de l'application publiée.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Publication
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent $PSScriptRoot

# Ces paquets ne livrent pas le texte de leur licence : il est repris du dépôt de leur projet, au commit qu'ils déclarent.
$licencesDuDepot = @{
    'AvalonEdit'                                            = 'AvalonEdit.txt'
    'Microsoft.Extensions.DependencyInjection'              = 'dotnet.txt'
    'Microsoft.Extensions.DependencyInjection.Abstractions' = 'dotnet.txt'
    'Microsoft.Extensions.Logging.Abstractions'             = 'dotnet.txt'
    'System.CommandLine'                                    = 'dotnet.txt'
    'System.Security.Cryptography.ProtectedData'            = 'dotnet.txt'
    'System.Speech'                                         = 'dotnet.txt'
    'WpfMath'                                               = 'XamlMath.txt'
    'XamlMath.Shared'                                       = 'XamlMath.txt'
    'YamlDotNet'                                            = 'YamlDotNet.txt'
}

$entete = @'
# Paquets NuGet publiés avec kyfRev

kyfRev est publié avec les paquets NuGet ci-dessous, runtime .NET compris, et leurs licences s'appliquent à ces parties.

Ce fichier est écrit par `tools/avis-nuget.ps1` à la construction de l'installateur : il ne se modifie pas à la main.
'@

# Quatre accents graves : FONT-LICENSES.md de WpfMath contient des blocs à trois.
$cloture = '````'
$dejaCites = [Collections.Generic.Dictionary[string, string]]::new()

# Le dossier du paquet, dans l'un des dossiers de paquets de la restauration.
function Find-Paquet([string] $Id, [string] $Version) {
    foreach ($depot in $depots) {
        $dossier = Join-Path $depot $Id.ToLowerInvariant() $Version
        if (Test-Path $dossier) { return $dossier }
    }
    throw "Le paquet $Id $Version est introuvable dans : $($depots -join ', ')."
}

function Read-Texte([string] $Chemin) {
    ((Get-Content -Raw -Encoding utf8 -LiteralPath $Chemin) -replace "`r`n?", "`n").Trim()
}

# Le texte sous son titre, ou un renvoi s'il figure déjà plus haut, tel quel.
function Format-Texte([string] $Titre, [string] $Texte, [string] $Paquet) {
    if ($dejaCites.ContainsKey($Texte)) {
        return "$Titre : même texte que pour $($dejaCites[$Texte]).`n"
    }
    $dejaCites[$Texte] = $Paquet
    return "$Titre :`n`n${cloture}text`n$Texte`n$cloture`n"
}

function Format-Paquet([string] $Id, [string] $Version) {
    $dossier = Find-Paquet $Id $Version
    $paquet = "$Id $Version"
    $sections = @("## $paquet`n")

    $nuspec = [xml](Get-Content -Raw -Encoding utf8 (Get-ChildItem $dossier -Filter *.nuspec | Select-Object -First 1).FullName)
    $declaree = $nuspec.SelectSingleNode("//*[local-name()='license' and @type='expression']")
    if ($null -ne $declaree) { $sections += "Licence : $($declaree.InnerText).`n" }
    $copyright = $nuspec.SelectSingleNode("//*[local-name()='copyright']")
    if ($null -ne $copyright) { $sections += "Copyright : $($copyright.InnerText)`n" }

    $fichiers = @(Get-ChildItem $dossier -File | Sort-Object Name)
    $licences = @($fichiers | Where-Object Name -Match '^licen[cs]e(\.\w+)?$')
    if ($licences.Count -gt 0) {
        $sections += $licences | ForEach-Object { Format-Texte "``$($_.Name)`` du paquet" (Read-Texte $_.FullName) $paquet }
    }
    elseif ($licencesDuDepot.ContainsKey($Id)) {
        $sections += Format-Texte 'Licence, reprise du dépôt du projet' (Read-Texte (Join-Path $racine 'installer\licences-nuget' $licencesDuDepot[$Id])) $paquet
    }
    else {
        throw "Le paquet $paquet ne livre pas le texte de sa licence : rangez-le dans installer\licences-nuget, et déclarez-le dans `$licencesDuDepot."
    }
    $sections += $fichiers | Where-Object Name -Match '^(notice|third-?party-?notices|font-licenses)(\.\w+)?$' |
        ForEach-Object { Format-Texte "``$($_.Name)`` du paquet" (Read-Texte $_.FullName) $paquet }
    $sections -join "`n"
}

$actifs = Get-Content -Raw (Join-Path $racine 'src\KyfRev.App\obj\project.assets.json') | ConvertFrom-Json -AsHashtable
$depots = @($actifs.packageFolders.Keys)
# L'application et, s'il est publié avec elle, kyfrev-coffre.
$paquets = @(Get-ChildItem $Publication -Filter '*.deps.json' | ForEach-Object {
        (Get-Content -Raw $_.FullName | ConvertFrom-Json -AsHashtable).libraries.GetEnumerator() |
            Where-Object { $_.Value.type -in 'package', 'runtimepack' } |
            ForEach-Object { $_.Key -replace '^runtimepack\.', '' }
    } | Sort-Object -Unique)
if ($paquets.Count -eq 0) { throw "Aucun paquet dans les .deps.json de $Publication." }

$avis = @($paquets | ForEach-Object {
        $id, $version = $_ -split '/'
        Format-Paquet $id $version
    })
$sortie = Join-Path $Publication 'licences'
New-Item -ItemType Directory -Force $sortie | Out-Null
$contenu = (($entete -replace "`r`n?", "`n") + "`n`n" + ($avis -join "`n")).TrimEnd() + "`n"
Set-Content -LiteralPath (Join-Path $sortie 'THIRD-PARTY-NOTICES-nuget.md') -Encoding utf8NoBOM -NoNewline -Value $contenu

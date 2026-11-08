#Requires -Version 7.0
<#
.SYNOPSIS
    Publie une version de kyfRev sur GitHub, sous l'identité de la personne qui lance le script.

.DESCRIPTION
    À lancer sur main, une fois la version datée dans le CHANGELOG et la CI de ce commit verte. Le script
    vérifie l'étiquette, la version et le CHANGELOG ; reprend l'installateur et la version portable construits
    par la CI pour ce commit ; contrôle leur version. Sans -Publier, il s'arrête là et dit ce qu'il ferait.
    Avec -Publier, il pose l'étiquette annotée, la pousse et crée la version GitHub avec ces fichiers.

.PARAMETER Publier
    Pose l'étiquette et publie. Sans ce paramètre, rien n'est modifié.
#>
[CmdletBinding()]
param(
    [switch] $Publier
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent $PSScriptRoot
Set-Location $racine

$version = & (Join-Path $PSScriptRoot 'verifier-version.ps1') | Select-Object -Last 1
$etiquette = "v$version"
if (git status --porcelain) { throw 'Le dossier de travail n''est pas propre.' }
git fetch --quiet origin --tags
$commit = (git rev-parse HEAD).Trim()
if ($commit -ne (git rev-parse origin/main).Trim()) { throw 'HEAD n''est pas le dernier commit de origin/main.' }
if (git tag --list $etiquette) { throw "L'étiquette $etiquette existe déjà." }

$runs = gh run list --commit $commit --branch main --workflow verification.yml --json databaseId,conclusion | ConvertFrom-Json
$verte = @($runs | Where-Object conclusion -EQ 'success') | Select-Object -First 1
if (-not $verte) { throw "La CI de main n'est pas verte pour $commit : rien n'est publié." }

$sortie = Join-Path $racine "artifacts\publication\$version"
if (Test-Path $sortie) { Remove-Item -Recurse -Force $sortie }
New-Item -ItemType Directory -Force $sortie | Out-Null
foreach ($artefact in 'installateur', 'portable') {
    gh run download $verte.databaseId --name $artefact --dir $sortie
    if ($LASTEXITCODE -ne 0) { throw "L'artefact $artefact est introuvable (il est gardé 30 jours)." }
}
$installateur = Join-Path $sortie "kyfRev-$version-installation.exe"
$portable = Join-Path $sortie "kyfRev-$version-portable.zip"
foreach ($fichier in $installateur, $portable, "$portable.sha256") {
    if (-not (Test-Path $fichier)) { throw "$fichier est absent des artefacts de la CI." }
}
$construite = ((Get-Item $installateur).VersionInfo.ProductVersion -split '\+')[0].Trim()
if ($construite -ne $version) { throw "L'installateur est en version $construite, pas $version." }

$lignes = Get-Content (Join-Path $racine 'CHANGELOG.md')
$debut = [array]::FindIndex($lignes, [Predicate[string]] { param($l) $l -like "## ``[$version``] - *" })
$fin = [array]::FindIndex($lignes, $debut + 1, [Predicate[string]] { param($l) $l -match '^## \[|^\[[^\]]+\]: ' })
if ($fin -lt 0) { $fin = $lignes.Count }
$notes = Join-Path $sortie 'notes.md'
$lignes[($debut + 1)..($fin - 1)] | Set-Content $notes -Encoding utf8NoBOM
$options = @('release', 'create', $etiquette, $installateur, $portable, "$portable.sha256", '--verify-tag', '--title', "kyfRev $version", '--notes-file', $notes)
if ($version.Contains('-')) { $options += '--prerelease' }

if (-not $Publier) {
    Write-Host "Prêt : kyfRev $version, commit $commit, CI $($verte.databaseId)."
    Write-Host "Avec -Publier : étiquette $etiquette, puis gh $($options -join ' ')"
    return
}

git tag --annotate $etiquette --message "kyfRev $version"
if ($LASTEXITCODE -ne 0) { throw 'L''étiquette n''a pas pu être posée.' }
git push origin $etiquette
if ($LASTEXITCODE -ne 0) { throw 'L''étiquette n''a pas pu être poussée.' }
gh @options
if ($LASTEXITCODE -ne 0) { throw 'La version GitHub n''a pas pu être créée ; l''étiquette est posée.' }
Write-Host "kyfRev $version est publiée. Pensez à l'annonce signée : dotnet run tools/catalogue/catalogue.cs -- version <clé> $version"

#Requires -Version 7.0
<#
.SYNOPSIS
    Vérifie qu'un coffre écrit par cette version se rouvre dans kyfRev 0.x sans aucun signalement.

.DESCRIPTION
    Sur une copie du coffre (jamais sur le coffre lui-même) :
    1. kyfRev 0.x (crate coffre) l'ouvre et résume sa santé : c'est le point de départ ;
    2. cette version l'ouvre : même résumé, et rien n'est écrit ;
    3. cette version y fait une série d'écritures (fiches : création, enregistrement, étiquettes,
       ressource, titre, dossiers, déplacements, corbeille et restauration ; puis annotations,
       tirage du jour, examen, données d'extension, citations, schémas et modèle) ;
    4. kyfRev 0.x le rouvre : même résumé qu'au départ, et rien n'est réécrit.
    Prérequis : Rust et le dépôt kyfRev 0.x cloné à côté de ce dépôt (voir tools/reference-vectors).

.PARAMETER Coffre
    Coffre à copier ; par défaut, le coffre de référence des tests.
#>
[CmdletBinding()]
param(
    [string] $Coffre
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $Coffre) { $Coffre = Join-Path $racine 'tests\KyfRev.Infrastructure.Vault.Tests\fixtures\coffre-reference' }
$copie = Join-Path ([IO.Path]::GetTempPath()) "kyfrev-interop-$([guid]::NewGuid().ToString('N'))"
$manifeste = Join-Path $racine 'tools\reference-vectors\Cargo.toml'
$csharp = Join-Path $racine 'tools\interop\ecriture.cs'
$rust = Join-Path $racine 'tools\reference-vectors\target\release\reference-vectors.exe'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

# Empreinte de chaque fichier ; verrou-info.json (qui tient le coffre) change à chaque ouverture.
function Releve() {
    Get-ChildItem -Recurse -File $copie |
        Where-Object { $_.Name -ne 'verrou-info.json' } |
        ForEach-Object { "$([IO.Path]::GetRelativePath($copie, $_.FullName))|$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" } |
        Sort-Object
}

function Sante-Rust() {
    $sortie = & $rust --sante $copie
    if ($LASTEXITCODE -ne 0) { throw "kyfRev 0.x refuse le coffre : $sortie" }
    $sortie | Select-Object -First 1
}

Copy-Item -Recurse $Coffre $copie
$echecs = 0
try {
    cargo build --release --quiet --manifest-path $manifeste
    dotnet build $csharp | Out-Null

    Write-Host '1. kyfRev 0.x ouvre la copie :'
    $depart = Sante-Rust
    Write-Host "   $depart"

    Write-Host '2. Cette version l''ouvre et l''analyse :'
    $avant = Releve
    $resume = (dotnet run $csharp -- $copie analyse) | Select-Object -First 1
    if ($LASTEXITCODE -ne 0) { throw 'Ouverture impossible.' }
    if ($resume -ne $depart) { Write-Host "   ÉCHEC : santé différente.`n   C# : $resume"; $echecs++ } else { Write-Host '   même santé' }
    if (Compare-Object $avant (Releve)) { Write-Host '   ÉCHEC : l''ouverture a écrit dans le coffre.'; $echecs++ } else { Write-Host '   rien écrit' }

    Write-Host '3. Cette version y écrit :'
    $sortie = dotnet run $csharp -- $copie ecrire
    if ($LASTEXITCODE -ne 0) { throw "Écritures impossibles : $sortie" }
    Write-Host "   $sortie"

    Write-Host '4. kyfRev 0.x rouvre le coffre :'
    $avant = Releve
    $apres = Sante-Rust
    Write-Host "   $apres"
    if ($apres -ne $depart) { Write-Host '   ÉCHEC : nouveaux signalements.'; $echecs++ } else { Write-Host '   aucun nouveau signalement' }
    if (Compare-Object $avant (Releve)) { Write-Host '   ÉCHEC : kyfRev 0.x a dû corriger des suivis.'; $echecs++ } else { Write-Host '   rien réécrit' }
}
finally {
    Remove-Item -Recurse -Force $copie -ErrorAction SilentlyContinue
}
if ($echecs -gt 0) { exit 1 }
Write-Host 'Coffre écrit par cette version : relu par kyfRev 0.x sans aucun signalement.'

#Requires -Version 7.0
<#
.SYNOPSIS
    Vérifie que les sauvegardes de cette version et celles de kyfRev 0.x se restaurent l'une par
    l'autre, à l'identique.

.DESCRIPTION
    Sur une copie du coffre (jamais sur le coffre lui-même) :
    1. cette version la sauvegarde ; kyfRev 0.x restaure l'archive : chaque fichier doit être
       celui de la copie ;
    2. kyfRev 0.x la sauvegarde ; cette version restaure l'archive : même exigence.
    Le verrou du coffre (qui le tient, depuis quand) n'est jamais sauvegardé.
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
$essai = Join-Path ([IO.Path]::GetTempPath()) "kyfrev-sauvegardes-$([guid]::NewGuid().ToString('N'))"
$copie = Join-Path $essai 'coffre'
$manifeste = Join-Path $racine 'tools\reference-vectors\Cargo.toml'
$csharp = Join-Path $racine 'tools\interop\sauvegarde.cs'
$rust = Join-Path $racine 'tools\reference-vectors\target\release\reference-vectors.exe'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

# Empreinte de chaque fichier, verrou exclu.
function Releve([string] $dossier) {
    Get-ChildItem -Recurse -File -Force $dossier |
        Where-Object { $_.Name -notin 'verrou', 'verrou-info.json' } |
        ForEach-Object { "$([IO.Path]::GetRelativePath($dossier, $_.FullName))|$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" } |
        Sort-Object
}

function Comparer([string] $restaure, [string] $libelle) {
    $ecarts = Compare-Object (Releve $copie) (Releve $restaure)
    if ($ecarts) {
        Write-Host "   ÉCHEC : $libelle différent de la copie :"
        $ecarts | Select-Object -First 10 | ForEach-Object { Write-Host "     $($_.SideIndicator) $($_.InputObject)" }
        return 1
    }
    Write-Host "   $((Releve $copie).Count) fichiers identiques"
    return 0
}

New-Item -ItemType Directory $essai | Out-Null
Copy-Item -Recurse $Coffre $copie
$echecs = 0
try {
    cargo build --release --quiet --manifest-path $manifeste
    dotnet build $csharp | Out-Null

    Write-Host '1. Cette version sauvegarde, kyfRev 0.x restaure :'
    $archive = (dotnet run $csharp -- sauvegarder $copie (Join-Path $essai 'archives-net')) | Select-Object -First 1
    if ($LASTEXITCODE -ne 0) { throw "Sauvegarde impossible : $archive" }
    $restaure = (& $rust --restaurer $archive (Join-Path $essai 'restaure-par-0x')) | Select-Object -First 1
    if ($LASTEXITCODE -ne 0) { throw "kyfRev 0.x refuse l'archive : $restaure" }
    $echecs += Comparer $restaure 'le coffre restauré par kyfRev 0.x'

    Write-Host '2. kyfRev 0.x sauvegarde, cette version restaure :'
    $archive = (& $rust --sauvegarder $copie (Join-Path $essai 'archives-0x')) | Select-Object -First 1
    if ($LASTEXITCODE -ne 0) { throw "kyfRev 0.x ne sauvegarde pas : $archive" }
    $restaure = (dotnet run $csharp -- restaurer $archive (Join-Path $essai 'restaure-par-net')) | Select-Object -First 1
    if ($LASTEXITCODE -ne 0) { throw "Cette version refuse l'archive : $restaure" }
    $echecs += Comparer $restaure 'le coffre restauré par cette version'
}
finally {
    Remove-Item -Recurse -Force $essai -ErrorAction SilentlyContinue
}
if ($echecs -gt 0) { exit 1 }
Write-Host 'Sauvegardes : chaque version restaure à l''identique celles de l''autre.'

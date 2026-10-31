#Requires -Version 7.0
<#
.SYNOPSIS
    Vérifie que le verrou du coffre est respecté entre cette version et kyfRev 0.x, dans les deux sens.

.DESCRIPTION
    Sur une copie du coffre de référence :
    1. cette version tient le verrou : kyfRev 0.x (crate coffre) doit refuser d'ouvrir le coffre ;
    2. kyfRev 0.x tient le verrou : cette version doit refuser et nommer le processus qui le tient.
    Prérequis : Rust et le dépôt kyfRev 0.x cloné à côté de ce dépôt (voir tools/reference-vectors).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$reference = Join-Path $racine 'tests\KyfRev.Infrastructure.Vault.Tests\fixtures\coffre-reference'
$coffre = Join-Path ([IO.Path]::GetTempPath()) "kyfrev-interop-$([guid]::NewGuid().ToString('N'))"
$manifeste = Join-Path $racine 'tools\reference-vectors\Cargo.toml'
$csharp = Join-Path $racine 'tools\interop\verrou.cs'

function Attendre-Ouverture($processus, $sortie) {
    for ($i = 0; $i -lt 300 -and -not (Select-String -Path $sortie -Pattern '^ouvert' -Quiet -ErrorAction SilentlyContinue); $i++) {
        if ($processus.HasExited) { throw "Le processus s'est arrêté avant de prendre le verrou : $(Get-Content $sortie -Raw)" }
        Start-Sleep -Milliseconds 200
    }
}

Copy-Item -Recurse $reference $coffre
$echecs = 0
try {
    cargo build --release --quiet --manifest-path $manifeste
    dotnet build $csharp | Out-Null
    $rust = Join-Path $racine 'tools\reference-vectors\target\release\reference-vectors.exe'

    Write-Host '1. Cette version tient le verrou ; kyfRev 0.x tente d''ouvrir le coffre :'
    $sortie = New-TemporaryFile
    $tenant = Start-Process dotnet -ArgumentList @('run', $csharp, '--', $coffre, '30') -RedirectStandardOutput $sortie -PassThru -NoNewWindow
    Attendre-Ouverture $tenant $sortie
    $resultat = & $rust --ouvrir $coffre 0
    Write-Host "   $resultat"
    if ($LASTEXITCODE -ne 3) { Write-Host '   ÉCHEC : kyfRev 0.x a ouvert le coffre.'; $echecs++ } else { Write-Host '   conforme' }
    Stop-Process -Id $tenant.Id -Force -ErrorAction SilentlyContinue
    $tenant.WaitForExit()

    Write-Host '2. kyfRev 0.x tient le verrou ; cette version tente d''ouvrir le coffre :'
    $sortie = New-TemporaryFile
    $tenant = Start-Process $rust -ArgumentList @('--ouvrir', $coffre, '30') -RedirectStandardOutput $sortie -PassThru -NoNewWindow
    Attendre-Ouverture $tenant $sortie
    $resultat = dotnet run $csharp -- $coffre 0
    Write-Host "   $resultat"
    if ($LASTEXITCODE -ne 3 -or "$resultat" -notmatch "processus $($tenant.Id)") { Write-Host '   ÉCHEC : verrou non respecté ou détenteur non nommé.'; $echecs++ } else { Write-Host '   conforme' }
    Stop-Process -Id $tenant.Id -Force -ErrorAction SilentlyContinue
}
finally {
    Remove-Item -Recurse -Force $coffre -ErrorAction SilentlyContinue
}
if ($echecs -gt 0) { exit 1 }
Write-Host 'Verrou respecté dans les deux sens.'

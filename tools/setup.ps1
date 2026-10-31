#Requires -Version 7.0
<#
.SYNOPSIS
    Prépare un poste de développement kyfRev.

.DESCRIPTION
    - identité git du dépôt (KyllianFF) et crochets versionnés dans .githooks ;
    - gitleaks, version figée et empreinte SHA-256 vérifiée, dans .tools/ (ignoré par git) ;
    - outils .NET locaux déclarés dans .config/dotnet-tools.json.
    Le script peut être relancé sans risque : il ne refait que ce qui manque.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent $PSScriptRoot
$gitleaksVersion = '8.30.1'
$gitleaksEmpreinte = 'd29144deff3a68aa93ced33dddf84b7fdc26070add4aa0f4513094c8332afc4e'

function Invoke-Git {
    git -C $racine @args
    if ($LASTEXITCODE -ne 0) { throw "git $args a échoué." }
}

Write-Host 'Identité git et crochets du dépôt...'
Invoke-Git config user.name 'KyllianFF'
Invoke-Git config user.email '113480351+KyllianFF@users.noreply.github.com'
Invoke-Git config core.hooksPath .githooks
Invoke-Git config core.autocrlf false
Invoke-Git config pull.ff only

$dossierGitleaks = Join-Path $racine '.tools\gitleaks'
$exeGitleaks = Join-Path $dossierGitleaks 'gitleaks.exe'
$versionInstallee = if (Test-Path $exeGitleaks) { (& $exeGitleaks version) -replace '^v', '' } else { '' }

if ($versionInstallee -ne $gitleaksVersion) {
    Write-Host "Installation de gitleaks $gitleaksVersion dans .tools\gitleaks..."
    $archive = "gitleaks_${gitleaksVersion}_windows_x64.zip"
    $temporaire = Join-Path ([IO.Path]::GetTempPath()) $archive
    Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/gitleaks/gitleaks/releases/download/v$gitleaksVersion/$archive" -OutFile $temporaire
    $empreinte = (Get-FileHash -Algorithm SHA256 -Path $temporaire).Hash.ToLowerInvariant()
    if ($empreinte -ne $gitleaksEmpreinte) {
        Remove-Item $temporaire
        throw "Empreinte de $archive inattendue ($empreinte) : téléchargement rejeté."
    }
    if (Test-Path $dossierGitleaks) { Remove-Item -Recurse -Force $dossierGitleaks }
    Expand-Archive -Path $temporaire -DestinationPath $dossierGitleaks
    Remove-Item $temporaire
}

Write-Host 'Outils .NET locaux...'
Push-Location $racine
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore a échoué.' }
}
finally {
    Pop-Location
}

Write-Host 'Poste prêt.'

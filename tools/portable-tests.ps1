#Requires -Version 7.0
<#
.SYNOPSIS
    Essaie la version portable de kyfRev : à lancer en intégration continue ou dans Windows Sandbox.

.DESCRIPTION
    L'archive est décompressée dans un dossier temporaire : sa structure et ses licences sont contrôlées,
    puis kyfRev est lancé. Il doit démarrer en mode « portable », écrire son journal dans son propre
    dossier, et ne rien écrire dans les dossiers kyfRev du profil.

.PARAMETER Archive
    L'archive à essayer (résultat de tools/portable.ps1).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Archive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Vrai([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw "Échec : $Message." }
    Write-Host "  ok : $Message"
}

function Get-Profil {
    foreach ($dossier in (Join-Path $env:LOCALAPPDATA 'kyfRev'), (Join-Path $env:APPDATA 'kyfRev')) {
        if (Test-Path $dossier) {
            Get-ChildItem $dossier -Recurse -Force -File | ForEach-Object { '{0}|{1}|{2:o}' -f $_.FullName, $_.Length, $_.LastWriteTimeUtc }
        }
    }
}

$Archive = (Resolve-Path $Archive).Path
$essai = Join-Path ([IO.Path]::GetTempPath()) ('kyfrev-portable-' + [guid]::NewGuid().ToString('N'))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($Archive, $essai)

try {
    $racine = Join-Path $essai 'MesRévisions'
    $application = Join-Path $racine 'Application'
    Write-Host '1. Contenu de l''archive'
    foreach ($fichier in 'kyfRev.exe', 'portable.json', 'libsodium.dll', 'vcruntime140.dll', 'licences\LICENSE.txt', 'licences\THIRD-PARTY-NOTICES-nuget.md') {
        Assert-Vrai (Test-Path (Join-Path $application $fichier)) "$fichier présent"
    }
    Assert-Vrai (Test-Path (Join-Path $racine 'LISEZMOI.txt')) 'LISEZMOI.txt présent'
    Assert-Vrai ((Get-Content -Raw (Join-Path $application 'portable.json') | ConvertFrom-Json).coffre -eq '../Coffre') 'le coffre sera MesRévisions\Coffre'
    Assert-Vrai (@(Get-ChildItem $application -Recurse -Filter *.pdb).Count -eq 0) 'aucun fichier de symboles'
    Assert-Vrai (-not (Test-Path (Join-Path $application 'installation.json'))) 'pas de marque d''installation'
    $attendue = ((Get-Content -Raw "$Archive.sha256") -split '\s+')[0]
    Assert-Vrai ((Get-FileHash $Archive -Algorithm SHA256).Hash -eq $attendue) 'l''empreinte de l''archive correspond'

    Write-Host '2. Lancement'
    $avant = @(Get-Profil)
    $version = ((Get-Item (Join-Path $application 'kyfRev.exe')).VersionInfo.ProductVersion -split '\+')[0].Trim()
    $journal = Join-Path $application 'kyfRev.log'
    $attendu = "Démarrage de kyfRev $version (portable)"
    $kyfrev = Start-Process -FilePath (Join-Path $application 'kyfRev.exe') -PassThru
    try {
        $limite = [DateTime]::UtcNow.AddMinutes(1)
        while (-not ((Test-Path $journal) -and (Select-String -Path $journal -SimpleMatch $attendu -Quiet))) {
            if ($kyfrev.HasExited -or [DateTime]::UtcNow -gt $limite) { throw 'Échec : kyfRev n''a pas démarré en mode portable.' }
            Start-Sleep -Milliseconds 250
        }
        Assert-Vrai $true 'démarrage en mode portable, inscrit dans son propre dossier'
        Start-Sleep -Seconds 3
    }
    finally {
        $kyfrev.Kill($true)
        $kyfrev.WaitForExit()
    }
    $apres = @(Get-Profil)
    Assert-Vrai (($avant -join "`n") -eq ($apres -join "`n")) 'rien d''écrit dans les dossiers kyfRev du profil'
}
finally {
    $limite = [DateTime]::UtcNow.AddSeconds(20)
    while ((Test-Path $essai) -and [DateTime]::UtcNow -lt $limite) {
        try { Remove-Item -Recurse -Force $essai }
        catch [IO.IOException] { Start-Sleep -Milliseconds 500 }
    }
}
Write-Host 'Version portable : essais réussis.'

#Requires -Version 7.0
<#
.SYNOPSIS
    Fait tourner tools/verification-locale.ps1 dans Windows Sandbox : un Windows vierge et jetable.

.DESCRIPTION
    Le plus proche de l'intégration continue sur ce poste, pour un commit donné : un poste vierge, où
    Smart App Control ne bloque pas les assemblys tout juste compilées, et où tout tourne, y compris
    les scénarios qui tapent au clavier et l'essai complet de l'installateur. Les touches sont tapées
    dans le bac à sable : le poste reste utilisable pendant ce temps, tant que sa fenêtre reste ouverte.
    Évitez seulement de cliquer dedans et de la réduire.

    Sur le poste, le script clone le commit en cours, y reporte les modifications non validées du dossier
    de travail, et ouvre le bac à sable. Le bilan signale ces modifications : il ne vaut alors que pour
    elles, et non pour le commit. Le bac à sable voit en lecture seule le clone, .NET, PowerShell, Git,
    Node, Inno Setup et le cache NuGet, et n'écrit que dans artifacts/verification-sandbox. Il se referme
    tout seul à la fin ; rien n'en reste, et rien n'a changé sur le poste.

.PARAMETER Garder
    Laisse le bac à sable ouvert à la fin, pour regarder ce qui s'est passé.

.PARAMETER MinutesMax
    Passé ce délai, le bac à sable est refermé et la vérification est un échec.

.PARAMETER Invite
    Usage interne : la partie qui tourne dans le bac à sable.

.PARAMETER Base
    Ce à quoi comparer les commits pour la gouvernance ; par défaut, origin/main du poste.
#>
[CmdletBinding()]
param(
    [switch] $Garder,
    [int] $MinutesMax = 45,
    [switch] $Invite,
    [string] $Base
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Les messages des outils sont en français : ils se lisent en UTF-8.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$hoteSource = 'C:\host\source'
$hoteSortie = 'C:\host\sortie'

# Dans le bac à sable : copie le clone sur un disque où écrire, vérifie, range le résultat, se referme.
function Invoke-Invite {
    $code = 1
    try {
        $env:PATH = 'C:\host\dotnet;C:\host\pwsh;C:\host\git\cmd;C:\host\git\bin;C:\host\node;' + $env:PATH
        $env:DOTNET_ROOT = 'C:\host\dotnet'
        $env:DOTNET_NOLOGO = 'true'
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        $env:NUGET_FALLBACK_PACKAGES = 'C:\host\nuget'
        $env:PSExecutionPolicyPreference = 'Bypass'
        $depot = 'C:\kyfrev'
        & robocopy $hoteSource $depot /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "La copie du clone a échoué (code $LASTEXITCODE)." }
        Set-Location $depot
        # Les outils locaux viennent du cache du poste, déjà vérifiés : le réseau du bac à sable coupe parfois les gros
        # téléchargements (vu le 2026-11-06 : dotnet-stryker interrompu à chaque essai, et dotnet tool restore sans fin).
        $outils = Get-Content (Join-Path $depot '.config\dotnet-tools.json') -Raw | ConvertFrom-Json
        $paquets = Join-Path $env:USERPROFILE '.nuget\packages'
        foreach ($outil in $outils.tools.PSObject.Properties) {
            $nom = $outil.Name.ToLowerInvariant()
            $cache = Join-Path 'C:\host\nuget' (Join-Path $nom $outil.Value.version)
            if (Test-Path $cache) {
                & robocopy $cache (Join-Path $paquets (Join-Path $nom $outil.Value.version)) /E /NFL /NDL /NJH /NJS /NP | Out-Null
                if ($LASTEXITCODE -ge 8) { throw "La copie de l'outil $nom a échoué (code $LASTEXITCODE)." }
            }
        }
        & pwsh -NoProfile -File (Join-Path $depot 'tools\verification-locale.ps1') -Base $Base -PosteVierge *> (Join-Path $hoteSortie 'sortie.log')
        $code = $LASTEXITCODE
        foreach ($dossier in 'artifacts\coverage', 'artifacts\verification-locale', 'artifacts\installer\essais') {
            if (Test-Path (Join-Path $depot $dossier)) {
                Copy-Item (Join-Path $depot $dossier) (Join-Path $hoteSortie (Split-Path $dossier -Leaf)) -Recurse -Force
            }
        }
    }
    catch {
        Add-Content (Join-Path $hoteSortie 'sortie.log') "Le bac à sable a échoué : $($_.Exception.Message)"
    }
    finally {
        Set-Content (Join-Path $hoteSortie 'code.txt') $code
        Set-Content (Join-Path $hoteSortie 'fin.txt') (Get-Date -Format s)
        # Le poste le referme dès qu'il voit la fin : éteint de l'intérieur, il afficherait une erreur.
        # Ce délai ne sert que si le poste n'est plus là pour le faire.
        if (-not $Garder) { shutdown.exe /s /t 120 }
    }
}

function Stop-BacASable {
    wsb list 2>$null | Where-Object { $_.Trim() } | ForEach-Object { wsb stop --id $_.Trim() 2>$null | Out-Null }
}

# Un dossier du poste, vu en lecture seule dans le bac à sable.
function Format-Partage([string] $Poste, [string] $Invite, [bool] $Lecture = $true) {
    $hote = [Security.SecurityElement]::Escape($Poste)
    "    <MappedFolder><HostFolder>$hote</HostFolder><SandboxFolder>$Invite</SandboxFolder><ReadOnly>$($Lecture.ToString().ToLowerInvariant())</ReadOnly></MappedFolder>"
}

if ($Invite) {
    Invoke-Invite
    exit
}

$racine = Split-Path -Parent $PSScriptRoot
$bac = Join-Path $env:windir 'System32\WindowsSandbox.exe'
if (-not (Test-Path $bac)) { throw 'Windows Sandbox n''est pas activé sur ce poste (Fonctionnalités de Windows).' }
if (wsb list 2>$null) { throw 'Un bac à sable est déjà ouvert, et Windows n''en permet qu''un : refermez-le.' }

$commit = git -C $racine rev-parse HEAD
$branche = git -C $racine branch --show-current
if (-not $Base) { $Base = git -C $racine rev-parse origin/main }
$travail = Join-Path $racine "artifacts\verification-sandbox\$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$source = Join-Path $travail 'source'
$sortie = Join-Path $travail 'sortie'
New-Item -ItemType Directory -Force $sortie | Out-Null

Write-Host "Clone de $($commit.Substring(0, 7))..."
git clone --quiet --no-hardlinks $racine $source
if ($LASTEXITCODE -ne 0) { throw 'Le clone a échoué.' }
if ($branche) { git -C $source checkout --quiet -B $branche $commit } else { git -C $source checkout --quiet --detach $commit }
if ($LASTEXITCODE -ne 0) { throw "Le commit $commit est introuvable dans le clone." }
$modifications = Join-Path $travail 'modifications.diff'
git -C $racine diff HEAD --binary "--output=$modifications"
if ((Get-Item $modifications).Length -gt 0) {
    git -C $source apply --whitespace=nowarn $modifications
    if ($LASTEXITCODE -ne 0) { throw 'Les modifications non validées ne s''appliquent pas au clone.' }
}
foreach ($fichier in git -C $racine ls-files --others --exclude-standard) {
    $cible = Join-Path $source $fichier
    New-Item -ItemType Directory -Force (Split-Path -Parent $cible) | Out-Null
    Copy-Item (Join-Path $racine $fichier) $cible -Force
}
New-Item -ItemType Directory -Force (Join-Path $source '.tools') | Out-Null
Copy-Item (Join-Path $racine '.tools\gitleaks') (Join-Path $source '.tools\gitleaks') -Recurse -Force

# git.exe peut venir de cmd, de bin ou de mingw64\bin : la racine de Git est celle qui contient bin\sh.exe.
$racineGit = Split-Path -Parent (Get-Command git).Source
while ($racineGit -and -not (Test-Path (Join-Path $racineGit 'bin\sh.exe'))) { $racineGit = Split-Path -Parent $racineGit }
if (-not $racineGit) { throw 'sh.exe est introuvable : installez Git pour Windows.' }

$partages = @(
    (Format-Partage (Split-Path -Parent (Get-Command dotnet).Source) 'C:\host\dotnet'),
    (Format-Partage $PSHOME 'C:\host\pwsh'),
    (Format-Partage $racineGit 'C:\host\git'),
    (Format-Partage (Split-Path -Parent (Get-Command node).Source) 'C:\host\node'),
    (Format-Partage ($env:NUGET_PACKAGES ?? (Join-Path $env:USERPROFILE '.nuget\packages')) 'C:\host\nuget'),
    (Format-Partage $source $hoteSource),
    (Format-Partage $sortie $hoteSortie $false)
)
$inno = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6'
if (Test-Path $inno) { $partages += Format-Partage $inno 'C:\Users\WDAGUtilityAccount\AppData\Local\Programs\Inno Setup 6' }

$options = "-Invite -Base $Base$(if ($Garder) { ' -Garder' })"
$configuration = @"
<Configuration>
  <Networking>Enable</Networking>
  <AudioInput>Disable</AudioInput>
  <VideoInput>Disable</VideoInput>
  <PrinterRedirection>Disable</PrinterRedirection>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MemoryInMB>16384</MemoryInMB>
  <MappedFolders>
$($partages -join "`n")
  </MappedFolders>
  <LogonCommand>
    <Command>C:\host\pwsh\pwsh.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Minimized -File $hoteSource\tools\verification-sandbox.ps1 $options</Command>
  </LogonCommand>
</Configuration>
"@
$fichier = Join-Path $travail 'verification.wsb'
Set-Content -LiteralPath $fichier -Value $configuration -Encoding utf8

Write-Host 'Ouverture de Windows Sandbox. Évitez de cliquer dans sa fenêtre ou de la réduire : vous pouvez travailler à côté.'
Write-Host "Suivi en direct : $(Join-Path $sortie 'sortie.log')"
Start-Process -FilePath $bac -ArgumentList "`"$fichier`""

$fin = Join-Path $sortie 'fin.txt'
$limite = [DateTime]::UtcNow.AddMinutes($MinutesMax)
while (-not (Test-Path $fin) -and [DateTime]::UtcNow -lt $limite) { Start-Sleep -Seconds 5 }
$journal = Join-Path $sortie 'sortie.log'
if (-not (Test-Path $fin)) {
    Stop-BacASable
    if (Test-Path $journal) { Get-Content $journal -Tail 40 | Write-Host }
    throw "La vérification n'a pas fini en $MinutesMax minutes : le bac à sable est refermé."
}

$lignes = @(Get-Content $journal)
$debut = [Array]::LastIndexOf($lignes, '=== Bilan')
if ($debut -ge 0) { $lignes[$debut..($lignes.Count - 1)] | Write-Host } else { $lignes | Select-Object -Last 40 | Write-Host }
Write-Host "Résultats : $sortie"

if (-not $Garder) {
    Stop-BacASable
    # Le clone reste verrouillé tant que le bac à sable se referme.
    $limite = [DateTime]::UtcNow.AddMinutes(1)
    while ((wsb list 2>$null) -and [DateTime]::UtcNow -lt $limite) { Start-Sleep -Seconds 2 }
    Remove-Item $source -Recurse -Force -ErrorAction SilentlyContinue
}
exit ([int](Get-Content (Join-Path $sortie 'code.txt') -Raw).Trim())

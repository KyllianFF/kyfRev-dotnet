#Requires -Version 7.0
<#
.SYNOPSIS
    Refait sur ce poste ce que fait l'intégration continue, quand elle ne peut pas tourner.

.DESCRIPTION
    Les étapes de .github/workflows/verification.yml, dans le même ordre, sur le dossier de travail :
    gouvernance, compilation, tests et couverture, qualité, éditeur visuel, installateur. Une étape
    qui échoue n'arrête pas les suivantes : le bilan final dit tout. Le journal complet est écrit dans
    artifacts/verification-locale.

    Ce qui diffère d'un serveur de vérification :
    - Smart App Control peut refuser une assembly tout juste compilée : le projet de tests touché est
      relancé en Debug. Si elle reste refusée, le bilan le marque « non vérifié », car cela ne dit rien
      du code, et « Tests en échec » liste ce qui n'a pas pu tourner ;
    - les scénarios qui tapent au clavier (T5, T11, T14, T17) demandent une session ouverte et un poste
      auquel personne ne touche. Session verrouillée, ils ne sont pas lancés, et le bilan le dit ;
    - l'installateur s'essaie sans raccourci et sans lancer kyfRev (pas de -PosteVierge). Il n'est pas
      essayé si kyfRev est installé pour cet utilisateur ou ouvert : l'essai le remplacerait, ou
      s'arrêterait. Si Smart App Control le bloque, ce n'est pas un défaut : le bilan le marque
      « non vérifié ».

.PARAMETER Base
    Ce à quoi comparer les commits pour la gouvernance.

.PARAMETER Sauter
    Les étapes à ne pas lancer. Le bilan les marque « non vérifié ».

.PARAMETER SansClavier
    Ne lance pas les scénarios qui tapent au clavier, pour continuer à se servir du poste.

.PARAMETER PosteVierge
    Poste sans kyfRev ni coffre, comme Windows Sandbox : l'essai de l'installateur lance kyfRev et crée
    ses raccourcis (tools/verification-sandbox.ps1).
#>
[CmdletBinding()]
param(
    [string] $Base = 'origin/main',
    [ValidateSet('Gouvernance', 'Construction', 'Tests', 'Qualite', 'Editeur', 'IlotSchemas', 'Installateur')]
    [string[]] $Sauter = @(),
    [switch] $SansClavier,
    [switch] $PosteVierge
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Les messages des outils sont en français : ils se lisent, et se reconnaissent, en UTF-8.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$racine = Split-Path -Parent $PSScriptRoot
Set-Location $racine
$cleInstallation = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{69A07F65-7110-4520-A128-737A61015928}_is1'
$scenariosAuClavier = 'T5_a_new_passage', 'T11_a_new_sheet', 'T14_a_change', 'T17_no_debugging', 'J8_nothing_of_an_encrypted'
$sortie = Join-Path $racine 'artifacts\verification-locale'
New-Item -ItemType Directory -Force $sortie | Out-Null
$journal = Join-Path $sortie ((Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$bilan = [Collections.Generic.List[object]]::new()
$echecsDeTests = [Collections.Generic.List[string]]::new()

function Write-Ligne([string] $Texte) {
    Write-Host $Texte
    Add-Content -LiteralPath $journal -Value $Texte
}

function Add-Bilan([string] $Etape, [string] $Resultat, [string] $Detail = '', [TimeSpan] $Duree = [TimeSpan]::Zero) {
    $bilan.Add([pscustomobject]@{ Etape = $Etape; Resultat = $Resultat; Duree = $Duree.ToString('mm\:ss'); Detail = $Detail })
}

# Lance une étape : ce qu'elle lève devient un échec dans le bilan, et la suite continue.
function Invoke-Etape([string] $Nom, [scriptblock] $Action) {
    Write-Ligne ''
    Write-Ligne "=== $Nom"
    $debut = [DateTime]::UtcNow
    try {
        & $Action | ForEach-Object { Write-Ligne "$_" }
        Add-Bilan $Nom 'réussi' '' ([DateTime]::UtcNow - $debut)
    }
    catch {
        Write-Ligne $_.Exception.Message
        Add-Bilan $Nom 'ÉCHEC' $_.Exception.Message ([DateTime]::UtcNow - $debut)
    }
}

# Lance un programme ; un code de sortie autre que zéro est une erreur.
function Exec {
    $programme, $arguments = $args
    & $programme @arguments 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) { throw "$programme a rendu le code $LASTEXITCODE." }
}

function Invoke-Gouvernance {
    # git.exe peut venir de cmd, de bin ou de mingw64\bin : sh.exe est dans le bin de la racine de Git.
    $racineGit = Split-Path -Parent (Get-Command git).Source
    while ($racineGit -and -not (Test-Path (Join-Path $racineGit 'bin\sh.exe'))) { $racineGit = Split-Path -Parent $racineGit }
    if (-not $racineGit) { throw 'sh.exe est introuvable : installez Git pour Windows.' }
    $sh = Join-Path $racineGit 'bin\sh.exe'
    $plage = "$Base..HEAD"
    Invoke-Etape 'Identité et messages des commits' { Exec $sh tools/git/check-commits.sh $plage }
    $branche = git branch --show-current
    if ($branche -eq 'main') {
        Invoke-Etape 'Fusions sur main' { Exec $sh tools/git/check-main-merges.sh $plage }
    }
    elseif ($branche) {
        Invoke-Etape 'Nom de la branche' { Exec $sh tools/git/check-branch-name.sh $branche }
    }
    Invoke-Etape 'Secrets dans tout l''historique' {
        $gitleaks = Join-Path $racine '.tools\gitleaks\gitleaks.exe'
        if (-not (Test-Path $gitleaks)) { throw 'gitleaks manque : lancez tools/setup.ps1.' }
        Exec $gitleaks git --redact --no-banner .
    }
}

function Invoke-Construction {
    Invoke-Etape 'Outils .NET locaux' { Exec dotnet tool restore }
    Invoke-Etape 'Restauration verrouillée' { Exec dotnet restore KyfRev.slnx --locked-mode }
    Invoke-Etape 'Compilation' { Exec dotnet build KyfRev.slnx --configuration Release --no-restore }
}

# Un projet de tests, une ligne de bilan. Refusé par Smart App Control, il repart en Debug.
function Invoke-TestsDuProjet([string] $Nom, [string] $Projet) {
    $debut = [DateTime]::UtcNow
    Write-Ligne ''
    Write-Ligne "=== Tests : $Nom"
    $options = @(
        '--no-build', '--coverlet', '--coverlet-output-format', 'cobertura',
        '--coverlet-threshold', '95', '--coverlet-threshold-type', 'line',
        '--results-directory', (Join-Path $racine 'artifacts\coverage'))
    if ($script:sansClavierEffectif -and $Nom -eq 'KyfRev.Acceptance.Tests') {
        $options += '--filter', (($scenariosAuClavier | ForEach-Object { "FullyQualifiedName!~$_" }) -join '&')
    }
    $detail = ''
    foreach ($configuration in 'Release', 'Debug') {
        if ($configuration -eq 'Debug') {
            Write-Ligne 'Smart App Control a refusé une assembly : nouvel essai en Debug.'
            $detail = 'relancé en Debug (Smart App Control)'
            if (-not $script:debugCompile) {
                dotnet build KyfRev.slnx --configuration Debug --no-restore 2>&1 | ForEach-Object { Write-Ligne "$_" }
                $script:debugCompile = $true
            }
        }
        $lignes = [Collections.Generic.List[string]]::new()
        dotnet test --project $Projet --configuration $configuration @options 2>&1 | ForEach-Object {
            $lignes.Add("$_")
            Write-Ligne "$_"
        }
        $code = $LASTEXITCODE
        $refuse = $lignes -match 'strat.gie de contr.le d|0x800711C7'
        if ($code -eq 0 -or -not $refuse) { break }
    }
    $lignes -match '^(.chec|failed) ' | ForEach-Object { $echecsDeTests.Add("$Nom : $_") }
    if ($code -eq 0) {
        Add-Bilan "Tests : $Nom" 'réussi' $detail ([DateTime]::UtcNow - $debut)
    }
    elseif ($refuse) {
        # Windows a empêché de charger le code : cela ne dit rien de lui, ni en bien ni en mal.
        Add-Bilan "Tests : $Nom" 'non vérifié' 'Smart App Control a bloqué une assembly, en Release comme en Debug' ([DateTime]::UtcNow - $debut)
    }
    else {
        # 14 : tous les tests passent, mais la couverture est sous le seuil.
        $raison = if ($code -eq 14) { 'couverture sous 95 %' } else { "code $code" }
        Add-Bilan "Tests : $Nom" 'ÉCHEC' (($raison, $detail) -ne '' -join ', ') ([DateTime]::UtcNow - $debut)
    }
}

function Invoke-Tests {
    $script:debugCompile = $false
    $script:sansClavierEffectif = $SansClavier -or [bool](Get-Process logonui -ErrorAction SilentlyContinue)
    Remove-Item (Join-Path $racine 'artifacts\coverage') -Recurse -Force -ErrorAction SilentlyContinue
    if (-not $script:sansClavierEffectif) {
        Write-Ligne ''
        Write-Ligne 'Les scénarios tapent de vraies touches : ne touchez ni au clavier ni à la souris jusqu''au bilan.'
    }
    # Un projet après l'autre : les scénarios ouvrent de vraies fenêtres, qui ne doivent pas se croiser.
    foreach ($dossier in Get-ChildItem tests -Directory) {
        $projet = Join-Path $dossier.FullName "$($dossier.Name).csproj"
        if (Test-Path $projet) { Invoke-TestsDuProjet $dossier.Name $projet }
    }
    if ($script:sansClavierEffectif) {
        Add-Bilan 'Scénarios au clavier (T5, T11, T14, T17)' 'non vérifié' 'session verrouillée ou -SansClavier'
    }
}

function Invoke-Qualite {
    Invoke-Etape 'Mise en forme' {
        $env:Configuration = 'Release'
        try {
            # L'outil plante parfois tout seul (violation d'accès) : seul un second plantage compte.
            $lignes = dotnet format KyfRev.slnx --verify-no-changes --no-restore 2>&1 | ForEach-Object { "$_" }
            if ($LASTEXITCODE -ne 0 -and $lignes -match 'AccessViolationException') {
                $lignes = dotnet format KyfRev.slnx --verify-no-changes --no-restore 2>&1 | ForEach-Object { "$_" }
            }
            $code = $LASTEXITCODE
            $lignes
            if ($code -ne 0) { throw "La mise en forme a rendu le code $code." }
        }
        finally {
            Remove-Item Env:Configuration
        }
    }
    Invoke-Etape 'Paquets vulnérables' {
        $json = dotnet list KyfRev.slnx package --vulnerable --include-transitive --format json
        if ($LASTEXITCODE -ne 0) { throw 'La liste des paquets a échoué.' }
        $vulnerables = @(($json | ConvertFrom-Json).projects |
            Where-Object { $_.PSObject.Properties['frameworks'] } |
            ForEach-Object { $_.frameworks } |
            ForEach-Object { @($_.PSObject.Properties['topLevelPackages']?.Value) + @($_.PSObject.Properties['transitivePackages']?.Value) } |
            Where-Object { $_ -and $_.PSObject.Properties['vulnerabilities'] })
        if ($vulnerables.Count -gt 0) { throw "Vulnérables : $(($vulnerables | ForEach-Object { "$($_.id) $($_.resolvedVersion)" }) -join ', ')." }
        'Aucun paquet vulnérable connu.'
    }
    Invoke-Etape 'Raccourcis interdits' { Exec dotnet slopwatch analyze --no-baseline --fail-on warning }
}

function Invoke-Editeur {
    Push-Location (Join-Path $racine 'tools\visual-editor')
    try {
        Invoke-Etape 'Éditeur : dépendances verrouillées' { Exec npm ci --ignore-scripts }
        Invoke-Etape 'Éditeur : types' { Exec npm run check }
        Invoke-Etape 'Éditeur : aller-retour Markdown' { Exec npm test }
        Invoke-Etape 'Éditeur : le paquet correspond à ses sources' {
            Exec npm run build
            $paquet = '../../src/KyfRev.Presentation.Wpf/Editing/Web', '../../src/KyfRev.Presentation.Wpf/Editing/THIRD-PARTY-NOTICES.md'
            $ecart = git status --porcelain -- @paquet
            if ($ecart) { throw "Le paquet versionné diffère de ses sources : $ecart" }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-IlotSchemas {
    Push-Location (Join-Path $racine 'tools\schema-editor')
    try {
        Invoke-Etape 'Îlot des schémas : dépendances verrouillées' { Exec npm ci --ignore-scripts }
        Invoke-Etape 'Îlot des schémas : types' { Exec npm run check }
        Invoke-Etape 'Îlot des schémas : topologie' { Exec npm test }
        Invoke-Etape 'Îlot des schémas : le paquet correspond à ses sources' {
            Exec npm run build
            $paquet = '../../src/KyfRev.Presentation.Wpf/Schemas/Web', '../../src/KyfRev.Presentation.Wpf/Schemas/THIRD-PARTY-NOTICES.md'
            $ecart = git status --porcelain -- @paquet
            if ($ecart) { throw "Le paquet versionné diffère de ses sources : $ecart" }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-Installateur {
    $chemin = $null
    Invoke-Etape 'Construction de l''installateur' {
        $lignes = @(& pwsh -NoProfile -File tools/installer.ps1 2>&1 | ForEach-Object { "$_" })
        if ($LASTEXITCODE -ne 0) { $lignes; throw "tools/installer.ps1 a rendu le code $LASTEXITCODE." }
        $lignes
        Set-Variable chemin $lignes[-1] -Scope 2
    }
    if (-not $chemin) { return }
    if (Test-Path $cleInstallation) {
        Add-Bilan 'Essai de l''installateur' 'non vérifié' 'kyfRev est installé pour cet utilisateur : l''essai le remplacerait'
    }
    elseif (Get-Process kyfRev -ErrorAction SilentlyContinue) {
        Add-Bilan 'Essai de l''installateur' 'non vérifié' 'kyfRev est ouvert : refermez-le'
    }
    else {
        $debut = Get-Date
        $script:refusVisible = $false
        Invoke-Etape 'Essai de l''installateur (installation, mise à jour, désinstallation)' {
            $vierge = @(if ($PosteVierge) { '-PosteVierge' })
            $lignes = @(& pwsh -NoProfile -File tools/installer-tests.ps1 -Installateur $chemin @vierge 2>&1 | ForEach-Object { "$_" })
            $code = $LASTEXITCODE
            $lignes
            $script:refusVisible = [bool]($lignes -match 'strat.gie de contr.le d')
            if ($code -ne 0) { throw "tools/installer-tests.ps1 a rendu le code $code." }
        }
        if ($bilan[-1].Resultat -eq 'ÉCHEC' -and (Get-Process kyfRev -ErrorAction SilentlyContinue)) {
            $bilan[-1].Resultat = 'non vérifié'
            $bilan[-1].Detail = 'kyfRev a été ouvert pendant l''essai : refermez-le'
        }
        elseif ($bilan[-1].Resultat -eq 'ÉCHEC' -and ($script:refusVisible -or (Test-RefusDeWindows $debut))) {
            $bilan[-1].Resultat = 'non vérifié'
            $bilan[-1].Detail = 'Smart App Control a bloqué l''installateur d''essai'
        }
    }
}

# Vrai si Windows a refusé un fichier de kyfRev depuis ce moment (3077 : le journal de l'intégrité du code).
function Test-RefusDeWindows([DateTime] $Depuis) {
    # Windows écrit son journal un instant après le refus.
    Start-Sleep -Seconds 3
    $filtre = @{ LogName = 'Microsoft-Windows-CodeIntegrity/Operational'; Id = 3077; StartTime = $Depuis }
    [bool](Get-WinEvent -FilterHashtable $filtre -ErrorAction SilentlyContinue | Where-Object Message -Match 'kyfRev')
}

function Invoke-Groupe([string] $Nom, [scriptblock] $Action) {
    if ($Nom -in $Sauter) { Add-Bilan $Nom 'non vérifié' 'étape sautée' }
    else { & $Action }
}

$commit = git rev-parse --short HEAD
$nonPropre = [bool](git status --porcelain)
Write-Ligne "Vérification locale de $commit$(if ($nonPropre) { ', avec des modifications non validées' })"

Invoke-Groupe 'Gouvernance' { Invoke-Gouvernance }
Invoke-Groupe 'Construction' { Invoke-Construction }
Invoke-Groupe 'Tests' { Invoke-Tests }
Invoke-Groupe 'Qualite' { Invoke-Qualite }
Invoke-Groupe 'Editeur' { Invoke-Editeur }
Invoke-Groupe 'IlotSchemas' { Invoke-IlotSchemas }
Invoke-Groupe 'Installateur' { Invoke-Installateur }

Write-Ligne ''
Write-Ligne '=== Bilan'
$bilan | Format-Table -AutoSize -Wrap | Out-String -Width 160 | ForEach-Object { Write-Ligne $_.TrimEnd() }
if ($echecsDeTests.Count -gt 0) {
    Write-Ligne 'Tests en échec :'
    $echecsDeTests | ForEach-Object { Write-Ligne "  $_" }
}
$couverture = @{}
foreach ($rapport in $(if ('Tests' -notin $Sauter) { Get-ChildItem (Join-Path $racine 'artifacts\coverage') -Filter *.xml -ErrorAction SilentlyContinue })) {
    # Un projet qui n'a rien mesuré n'a pas de paquet : la recherche n'en trouve simplement aucun.
    foreach ($module in ([xml](Get-Content -Raw $rapport.FullName)).SelectNodes('/coverage/packages/package')) {
        $taux = [double]::Parse($module.'line-rate', [Globalization.CultureInfo]::InvariantCulture) * 100
        if (-not $couverture.ContainsKey($module.name) -or $couverture[$module.name] -lt $taux) { $couverture[$module.name] = $taux }
    }
}
if ($couverture.Count -gt 0) {
    Write-Ligne 'Couverture des lignes :'
    $couverture.GetEnumerator() | Sort-Object Value | ForEach-Object { Write-Ligne ('  {0,6:N2} %  {1}' -f $_.Value, $_.Key) }
}
$echecs = @($bilan | Where-Object Resultat -EQ 'ÉCHEC').Count
$manques = @($bilan | Where-Object Resultat -EQ 'non vérifié').Count
Write-Ligne ''
Write-Ligne "$commit : $echecs échec(s), $manques étape(s) non vérifiée(s)$(if ($nonPropre) { ', modifications non validées : ce résultat ne vaut pas pour ce commit' })."
Write-Ligne "Journal : $journal"
exit ([int]($echecs -gt 0))

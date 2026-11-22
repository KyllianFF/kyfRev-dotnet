#Requires -Version 7.0
<#
.SYNOPSIS
    Essaie l'installateur de kyfRev pour l'utilisateur en cours, sans droits administrateur.

.DESCRIPTION
    Avec l'installateur donné et un installateur d'essai plus récent (construit par
    tools/installer.ps1), dans un dossier temporaire :
    1. installation sans raccourci, fiches dans AppData (/NOICONS, /COFFRE=donnees) : fichiers,
       licences, installation.json, inscription dans Paramètres › Applications ;
    2. kyfRev ouvert : une installation s'arrête sans rien modifier ;
    3. sur un poste vierge, kyfRev installé démarre en mode « installée » et se signale ouvert ;
       lancé pendant qu'une installation tourne (mutex kyfRevInstallation), il se referme sans rien ouvrir,
       sauf ouvert par l'installateur à la fin (--apres-installation) ;
    4. mise à jour par la version plus récente, avec les options que kyfRev lui passe : même dossier,
       mêmes choix, licences en place ; sur un poste vierge, /RELANCER rouvre la nouvelle version ;
    5. retour à la version d'origine refusé : code 7, rien n'est modifié ;
    6. désinstallation : le dossier, ses licences et l'inscription disparaissent, les données du
       profil restent ;
    7. sur un poste vierge, installation par défaut : raccourcis dans le menu Démarrer et sur le
       Bureau, fiches dans Documents ; la désinstallation retire les raccourcis ;
    8. sur un poste vierge, désinstallation avec /REGLAGES=supprimer : les réglages que kyfRev tient
       dans le profil sont retirés, pas un coffre, pas un fichier qui n'est pas à lui ; avec
       /REGLAGES=garder, comme sans rien demander, ils restent.
    Les journaux d'Inno Setup sont rangés dans artifacts/installer/essais.

.PARAMETER Installateur
    L'installateur à essayer (résultat de tools/installer.ps1).

.PARAMETER PosteVierge
    Poste sans kyfRev ni coffre, comme celui de l'intégration continue : kyfRev installé est
    lancé (il rouvrirait sinon le dernier coffre de l'utilisateur) et des raccourcis sont créés.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Installateur,
    [switch] $PosteVierge
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent $PSScriptRoot
$cle = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{69A07F65-7110-4520-A128-737A61015928}_is1'
$dossier = Join-Path ([IO.Path]::GetTempPath()) ('kyfrev-installation-' + [guid]::NewGuid().ToString('N'))
$journaux = Join-Path $racine 'artifacts\installer\essais'
$profil = Join-Path $env:LOCALAPPDATA 'kyfRev'
# Le dossier licences de l'installation : nom de chaque fichier, et fichier du dépôt dont il est la copie.
$licences = [ordered]@{
    'LICENSE.txt'                      = 'LICENSE'
    'THIRD-PARTY-NOTICES-rendu.md'     = 'src\KyfRev.Rendering\THIRD-PARTY-NOTICES.md'
    'THIRD-PARTY-NOTICES-editeur.md'   = 'src\KyfRev.Presentation.Wpf\Editing\THIRD-PARTY-NOTICES.md'
    'OFL-AtkinsonHyperlegibleMono.txt' = 'src\KyfRev.DesignSystem\Fonts\OFL-AtkinsonHyperlegibleMono.txt'
    'OFL-AtkinsonHyperlegibleNext.txt' = 'src\KyfRev.DesignSystem\Fonts\OFL-AtkinsonHyperlegibleNext.txt'
}
$Installateur = (Resolve-Path $Installateur).Path
$version = (Get-Item $Installateur).VersionInfo.ProductVersion.Trim()

if (Test-Path $cle) { throw 'kyfRev est déjà installé pour cet utilisateur : l''essai le remplacerait.' }
New-Item -ItemType Directory -Force $journaux | Out-Null

function Assert-Vrai([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw "Échec : $Message." }
    Write-Host "  ok : $Message"
}

# Chaque licence est installée, à l'identique du dépôt, et celles des polices ne sont pas en double.
function Assert-Licences {
    foreach ($licence in $licences.GetEnumerator()) {
        $installee = Join-Path $dossier 'licences' $licence.Key
        $identique = (Test-Path $installee) -and (Get-FileHash $installee).Hash -eq (Get-FileHash (Join-Path $racine $licence.Value)).Hash
        Assert-Vrai $identique "licences\$($licence.Key) installé, à l'identique du dépôt"
    }
    Assert-Vrai (-not (Test-Path (Join-Path $dossier 'Fonts'))) 'aucune copie des licences des polices ailleurs'

    $fichier = Join-Path $dossier 'licences\THIRD-PARTY-NOTICES-nuget.md'
    Assert-Vrai (Test-Path $fichier) 'licences\THIRD-PARTY-NOTICES-nuget.md installé'
    $avis = Get-Content -Raw -Encoding utf8 $fichier
    $deps = Get-Content -Raw (Join-Path $dossier 'kyfRev.deps.json') | ConvertFrom-Json -AsHashtable
    $paquets = @($deps.libraries.GetEnumerator() | Where-Object { $_.Value.type -in 'package', 'runtimepack' } |
            ForEach-Object { $_.Key -replace '^runtimepack\.', '' -replace '/', ' ' })
    $absents = @($paquets | Where-Object { -not $avis.Contains("## $_`n") })
    Assert-Vrai ($paquets.Count -gt 0 -and $absents.Count -eq 0) "avis NuGet : $($paquets.Count) paquets publiés, tous cités$(if ($absents) { " (absents : $($absents -join ', '))" })"
    # Ce qui ne vient pas d'une mention MIT ordinaire : WebView2 et ses tiers, les polices de WpfMath, les textes du dépôt.
    $textes = 'Redistributions in binary form', 'StringTemplate4', 'jlm_msam10.ttf', 'Knuth License', 'AvalonEdit Contributors', 'Antoine Aubry', 'Alex Regueiro', '.NET Runtime uses third-party libraries'
    $absents = @($textes | Where-Object { -not $avis.Contains($_) })
    Assert-Vrai ($absents.Count -eq 0) "avis NuGet : les textes attendus sont là$(if ($absents) { " (absents : $($absents -join ', '))" })"
}

$script:numero = 0
# Lance un installateur ou le désinstallateur en mode silencieux ; rend son code de sortie.
function Invoke-Silencieux([string] $Programme, [string[]] $Options = @()) {
    $script:numero++
    $journal = Join-Path $journaux "$script:numero-$([IO.Path]::GetFileNameWithoutExtension($Programme)).log"
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$journal`"") + $Options
    $processus = Start-Process -FilePath $Programme -ArgumentList $arguments -PassThru
    if (-not $processus.WaitForExit([TimeSpan]::FromMinutes(5))) {
        $processus.Kill()
        throw "Échec : $Programme ne s'est pas terminé à temps."
    }
    return $processus.ExitCode
}

function Get-Inscription { Get-ItemProperty $cle -ErrorAction SilentlyContinue }

# Vrai si le programme porte l'icône de kyfRev : à cet endroit, elle montre le vert de la fiche.
function Test-Icone([string] $Programme) {
    Add-Type -AssemblyName System.Drawing
    $icone = [Drawing.Icon]::ExtractAssociatedIcon($Programme)
    $image = $icone.ToBitmap()
    try {
        $point = $image.GetPixel([int]($image.Width / 4), [int]($image.Height * 19 / 32))
        return $point.A -eq 255 -and $point.G -gt $point.R -and $point.G -gt $point.B
    }
    finally {
        $image.Dispose()
        $icone.Dispose()
    }
}

# Vrai quand le second processus du désinstallateur a fermé ce journal : il y écrit en dernier, après avoir tout retiré.
function Test-DesinstallationFinie([string] $Journal) {
    (Test-Path -LiteralPath $Journal) -and
        (Select-String -LiteralPath $Journal -SimpleMatch '/SECONDPHASE' -Quiet) -and
        (Select-String -LiteralPath $Journal -SimpleMatch 'Log closed.' -Quiet)
}

# Désinstalle l'installation d'essai ; rend le code de sortie du désinstallateur. En nettoyage, après un échec, il
# n'affirme rien : l'erreur à montrer est celle qui a interrompu l'essai.
function Invoke-Desinstallation([string[]] $Options = @(), [switch] $Nettoyage) {
    $programme = (Get-Inscription).UninstallString.Trim('"')
    $code = Invoke-Silencieux $programme $Options
    # Le premier processus rend la main avant la fin : c'est un second, lancé depuis le dossier temporaire, qui retire le
    # dossier d'installation puis, après lui, les réglages demandés. La désinstallation n'est finie que quand ce second
    # processus ferme son journal ; regarder avant, c'est voir des réglages qu'il allait retirer.
    $journal = Join-Path $journaux "$script:numero-$([IO.Path]::GetFileNameWithoutExtension($programme)).log"
    $limite = [DateTime]::UtcNow.AddSeconds(30)
    while (((Test-Path $dossier) -or -not (Test-DesinstallationFinie $journal)) -and [DateTime]::UtcNow -lt $limite) { Start-Sleep -Milliseconds 250 }
    if (-not $Nettoyage) {
        Assert-Vrai (Test-DesinstallationFinie $journal) 'le désinstallateur a fini et fermé son journal'
    }
    return $code
}

function Get-Installation { Get-Content -Raw (Join-Path $dossier 'installation.json') }

# Raccourcis « kyfRev » de l'utilisateur qui mènent à l'installation d'essai.
function Get-Raccourcis {
    $shell = New-Object -ComObject WScript.Shell
    foreach ($lieu in [Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop')) {
        $lien = Join-Path $lieu 'kyfRev.lnk'
        if ((Test-Path $lien) -and $shell.CreateShortcut($lien).TargetPath.StartsWith($dossier, [StringComparison]::OrdinalIgnoreCase)) { $lien }
    }
}

# État des données de kyfRev dans le profil : chemin, taille et date de chaque fichier.
function Get-Profil {
    if (Test-Path $profil) {
        Get-ChildItem $profil -Recurse -File | ForEach-Object { "$($_.FullName)|$($_.Length)|$($_.LastWriteTimeUtc.Ticks)" }
    }
}

try {
    Write-Host "Installateur d'essai plus récent..."
    $recent = & (Join-Path $PSScriptRoot 'installer.ps1') -Version '99.0.0'

    Write-Host "1. Installation de kyfRev $version, sans raccourci, fiches dans AppData"
    $code = Invoke-Silencieux $Installateur @('/CURRENTUSER', "/DIR=`"$dossier`"", '/NOICONS', '/COFFRE=donnees')
    Assert-Vrai ($code -eq 0) "installation terminée (code $code)"
    Assert-Vrai (Test-Path (Join-Path $dossier 'kyfRev.exe')) 'kyfRev.exe installé'
    & (Join-Path $dossier 'kyfrev-coffre.exe') --help | Out-Null
    Assert-Vrai ($LASTEXITCODE -eq 0) 'kyfrev-coffre installé et utilisable'
    Assert-Vrai (Test-Icone (Join-Path $dossier 'kyfRev.exe')) 'kyfRev.exe porte l''icône de kyfRev'
    Assert-Vrai (Test-Icone $Installateur) 'l''installateur porte l''icône de kyfRev'
    Assert-Vrai (-not (Get-ChildItem $dossier -Filter *.pdb)) 'aucun fichier de symboles'
    Assert-Vrai ((Test-Path (Join-Path $dossier 'libsodium.dll')) -and (Test-Path (Join-Path $dossier 'vcruntime140.dll'))) 'libsodium.dll et le vcruntime140.dll qu''elle charge sont installés'
    Assert-Licences
    Assert-Vrai ((Get-Installation) -eq "{`"coffre`":`"donnees`"}`n") 'installation.json : fiches dans AppData'
    $inscription = Get-Inscription
    Assert-Vrai ($null -ne $inscription) 'inscrit dans Paramètres › Applications, pour l''utilisateur'
    Assert-Vrai ($inscription.DisplayName -eq 'kyfRev' -and $inscription.DisplayVersion -eq $version -and $inscription.Publisher -eq 'KyllianFF') "nom, version $version et éditeur KyllianFF"
    Assert-Vrai ($inscription.InstallLocation.TrimEnd('\') -eq $dossier) 'dossier d''installation inscrit'
    Assert-Vrai (-not (Get-Raccourcis)) 'aucun raccourci'

    Write-Host '2. kyfRev ouvert'
    $ouvert = [Threading.Mutex]::new($false, 'kyfRev')
    try {
        $code = Invoke-Silencieux $recent
    }
    finally {
        $ouvert.Dispose()
    }
    Assert-Vrai ($code -ne 0) "installation arrêtée (code $code)"
    Assert-Vrai ((Get-Inscription).DisplayVersion -eq $version) 'rien n''est modifié'

    if ($PosteVierge) {
        Write-Host '3. Lancement de kyfRev installé'
        $journalKyfRev = Join-Path $profil 'v1\kyfRev.log'
        $attendu = "Démarrage de kyfRev $version (installée)"
        $kyfrev = Start-Process -FilePath (Join-Path $dossier 'kyfRev.exe') -PassThru
        try {
            $limite = [DateTime]::UtcNow.AddMinutes(1)
            while (-not ((Test-Path $journalKyfRev) -and (Select-String -Path $journalKyfRev -SimpleMatch $attendu -Quiet))) {
                if ($kyfrev.HasExited -or [DateTime]::UtcNow -gt $limite) { throw 'Échec : kyfRev installé n''a pas démarré en mode installé.' }
                Start-Sleep -Milliseconds 250
            }
            Assert-Vrai $true 'démarrage en mode installé, inscrit dans le journal du profil'
            $signal = $null
            Assert-Vrai ([Threading.Mutex]::TryOpenExisting('kyfRev', [ref] $signal)) 'kyfRev se signale ouvert à l''installateur'
            $signal.Dispose()
        }
        finally {
            $kyfrev.Kill()
            $kyfrev.WaitForExit()
        }

        # Une installation tourne : kyfRev lancé à ce moment retiendrait des fichiers qu'elle remplace.
        $demarrages = @(Select-String -Path $journalKyfRev -SimpleMatch 'Démarrage de kyfRev').Count
        $installation = [Threading.Mutex]::new($false, 'kyfRevInstallation')
        try {
            $kyfrev = Start-Process -FilePath (Join-Path $dossier 'kyfRev.exe') -PassThru
            if (-not $kyfrev.WaitForExit([TimeSpan]::FromSeconds(30))) {
                $kyfrev.Kill()
                throw 'Échec : kyfRev lancé pendant une installation ne s''est pas refermé.'
            }
            Assert-Vrai (@(Select-String -Path $journalKyfRev -SimpleMatch 'Démarrage de kyfRev').Count -eq $demarrages) 'lancé pendant une installation, kyfRev se referme sans rien ouvrir'

            # Celui que l'installateur ouvre lui-même à la fin (case « Exécuter kyfRev », /RELANCER), mutex encore tenu.
            $kyfrev = Start-Process -FilePath (Join-Path $dossier 'kyfRev.exe') -ArgumentList '--apres-installation' -PassThru
            try {
                $limite = [DateTime]::UtcNow.AddMinutes(1)
                while (@(Select-String -Path $journalKyfRev -SimpleMatch 'Démarrage de kyfRev').Count -eq $demarrages) {
                    if ($kyfrev.HasExited -or [DateTime]::UtcNow -gt $limite) { throw 'Échec : kyfRev ouvert par l''installateur s''est refermé.' }
                    Start-Sleep -Milliseconds 250
                }
                Assert-Vrai $true 'ouvert par l''installateur à la fin, kyfRev démarre alors que l''installation tient encore son mutex'
            }
            finally {
                if (-not $kyfrev.HasExited) { $kyfrev.Kill() }
                $kyfrev.WaitForExit()
            }
        }
        finally {
            $installation.Dispose()
        }
    }

    Write-Host '4. Mise à jour en version 99.0.0, comme kyfRev la lance'
    # Les options de SilentInstaller : le mode de l'installation en place et, sur un poste vierge, /RELANCER.
    $options = @('/CURRENTUSER') + ($PosteVierge ? @('/RELANCER') : @())
    $code = Invoke-Silencieux $recent $options
    Assert-Vrai ($code -eq 0) "mise à jour terminée (code $code)"
    if ($PosteVierge) {
        $attendu = 'Démarrage de kyfRev 99.0.0 (installée)'
        $limite = [DateTime]::UtcNow.AddMinutes(1)
        while (-not (Select-String -Path $journalKyfRev -SimpleMatch $attendu -Quiet)) {
            if ([DateTime]::UtcNow -gt $limite) { throw 'Échec : kyfRev ne s''est pas rouvert après sa mise à jour.' }
            Start-Sleep -Milliseconds 250
        }
        Assert-Vrai $true 'avec /RELANCER, la nouvelle version se rouvre seule'
        $rouvert = Get-Process -Name 'kyfRev' -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($dossier, [StringComparison]::OrdinalIgnoreCase) }
        $rouvert | ForEach-Object { $_.Kill(); $_.WaitForExit() }
    }
    $inscription = Get-Inscription
    Assert-Vrai ($inscription.DisplayVersion -eq '99.0.0') 'version 99.0.0 inscrite'
    Assert-Vrai ($inscription.InstallLocation.TrimEnd('\') -eq $dossier) 'même dossier'
    Assert-Vrai ((Get-Item (Join-Path $dossier 'kyfRev.exe')).VersionInfo.ProductVersion -like '99.0.0*') 'kyfRev.exe remplacé'
    Assert-Licences
    Assert-Vrai ((Get-Installation) -eq "{`"coffre`":`"donnees`"}`n") 'fiches toujours dans AppData'
    Assert-Vrai (-not (Get-Raccourcis)) 'toujours aucun raccourci'

    Write-Host "5. Retour à la version $version"
    $code = Invoke-Silencieux $Installateur
    Assert-Vrai ($code -eq 7) "refusé avec le code 7 (code $code)"
    Assert-Vrai ((Get-Inscription).DisplayVersion -eq '99.0.0') 'version 99.0.0 toujours inscrite'
    Assert-Vrai ((Get-Item (Join-Path $dossier 'kyfRev.exe')).VersionInfo.ProductVersion -like '99.0.0*') 'kyfRev.exe intact'

    Write-Host '6. Désinstallation'
    $avant = Get-Profil
    $code = Invoke-Desinstallation
    Assert-Vrai ($code -eq 0) "désinstallation terminée (code $code)"
    Assert-Vrai ($null -eq (Get-Inscription)) 'plus inscrit dans Paramètres › Applications'
    Assert-Vrai (-not (Test-Path (Join-Path $dossier 'licences'))) 'licences retirées'
    Assert-Vrai (-not (Test-Path $dossier)) 'dossier d''installation retiré'
    Assert-Vrai ((@(Get-Profil) -join "`n") -eq (@($avant) -join "`n")) 'données du profil intactes'

    if ($PosteVierge) {
        Write-Host '7. Installation par défaut : raccourcis, fiches dans Documents'
        $code = Invoke-Silencieux $Installateur @('/CURRENTUSER', "/DIR=`"$dossier`"")
        Assert-Vrai ($code -eq 0) "installation terminée (code $code)"
        Assert-Vrai ((Get-Installation) -eq "{`"coffre`":`"documents`"}`n") 'installation.json : fiches dans Documents'
        Assert-Vrai (@(Get-Raccourcis).Count -eq 2) 'raccourcis dans le menu Démarrer et sur le Bureau'
        $code = Invoke-Desinstallation
        Assert-Vrai ($code -eq 0 -and -not (Test-Path $dossier)) "désinstallation terminée (code $code)"
        Assert-Vrai (-not (Get-Raccourcis)) 'raccourcis retirés'

        Write-Host '8. Désinstallation et réglages'
        # Ce que kyfRev tient dans le profil, un coffre rangé là par mégarde, un fichier qui n'est pas à lui, et un coffre dans Documents.
        $reglages = Join-Path $profil 'v1'
        $coffre = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'kyfRev\Coffre'
        function New-Reglages {
            foreach ($fichier in 'config-locale.json', 'preferences.json', 'seance.json', 'cles-retenues.json', 'kyfRev.log', 'WebView2\profil.txt', 'extensions\une\extension.json',
                'catalogue\cles-catalogue.json', 'mises-a-jour\etat.json', 'notes-a-moi.txt', 'Mon coffre\.suivi\coffre.json', 'Mon coffre\fiches\Fiche.md') {
                $chemin = Join-Path $reglages $fichier
                New-Item -ItemType Directory -Force (Split-Path $chemin) | Out-Null
                Set-Content -LiteralPath $chemin -Value 'essai' -Encoding utf8
            }
            New-Item -ItemType Directory -Force (Join-Path $coffre '.suivi'), (Join-Path $coffre 'fiches') | Out-Null
            Set-Content -LiteralPath (Join-Path $coffre '.suivi\coffre.json') -Value '{}' -Encoding utf8
            Set-Content -LiteralPath (Join-Path $coffre 'fiches\Fiche.md') -Value '# Fiche' -Encoding utf8
            Set-Content -LiteralPath (Join-Path $profil 'config-locale.json') -Value '{}' -Encoding utf8
        }
        function Get-Reste { @(Get-ChildItem $profil -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName.Substring($profil.Length + 1) } | Sort-Object) }

        $code = Invoke-Silencieux $Installateur @('/CURRENTUSER', "/DIR=`"$dossier`"", '/NOICONS')
        Assert-Vrai ($code -eq 0) "installation terminée (code $code)"
        New-Reglages
        $avant = Get-Reste
        $code = Invoke-Desinstallation @('/REGLAGES=garder')
        Assert-Vrai ($code -eq 0 -and -not (Test-Path $dossier)) "désinstallation terminée (code $code)"
        Assert-Vrai (((Get-Reste) -join '|') -eq ($avant -join '|')) '/REGLAGES=garder : les réglages restent, tous'

        $code = Invoke-Silencieux $Installateur @('/CURRENTUSER', "/DIR=`"$dossier`"", '/NOICONS')
        Assert-Vrai ($code -eq 0) "installation terminée (code $code)"
        $code = Invoke-Desinstallation @('/REGLAGES=supprimer')
        Assert-Vrai ($code -eq 0 -and -not (Test-Path $dossier)) "désinstallation terminée (code $code)"
        $reste = Get-Reste
        Assert-Vrai (($reste -join '|') -eq 'v1\Mon coffre\.suivi\coffre.json|v1\Mon coffre\fiches\Fiche.md|v1\notes-a-moi.txt') "/REGLAGES=supprimer : il ne reste que le coffre et le fichier qui ne sont pas des réglages ($($reste -join ', '))"
        Assert-Vrai ((Test-Path (Join-Path $coffre '.suivi\coffre.json')) -and (Test-Path (Join-Path $coffre 'fiches\Fiche.md'))) 'le coffre des Documents est intact'
        Remove-Item -LiteralPath $profil, (Split-Path $coffre) -Recurse -Force
    }
    Write-Host 'Installateur conforme.'
}
finally {
    # Un essai interrompu ne laisse pas kyfRev installé.
    $reste = Get-Inscription
    if ($reste -and $reste.InstallLocation.TrimEnd('\') -eq $dossier) {
        Invoke-Desinstallation -Nettoyage | Out-Null
    }
}

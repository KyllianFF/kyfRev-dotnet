; Installateur de kyfRev (plan de refonte, §5.5), pour Inno Setup 6.
;
; tools/installer.ps1 publie l'application (autonome, Windows x64), puis compile ce script en
; passant à ISCC :
;   /DVersion=1.0.0-alpha.1   version SemVer, dont la source unique est Directory.Build.props ;
;   /DNumericVersion=1.0.0.0  la même, sous la forme numérique des métadonnées de Windows ;
;   /DReleaseDate=…           date de publication en toutes lettres (« 1er novembre 2026 »), tirée
;                             de la section datée du CHANGELOG ; absente si la version n'est pas
;                             publiée ;
;   /DSource=…                dossier de l'application publiée ;
;   /DSignTool=…              facultatif : outil de signature déclaré à ISCC par /S.
;
; Comme l'installeur de la 0.x :
; - installation pour l'utilisateur en cours par défaut, sans droits administrateur
;   (%LOCALAPPDATA%\Programs\kyfRev), ou pour tous dans Program Files, au choix (/ALLUSERS) ;
; - raccourcis dans le menu Démarrer et sur le Bureau, au choix ; /NOICONS n'en crée aucun ;
; - emplacement des fiches de chaque personne, Documents\kyfRev ou AppData (/COFFRE=documents
;   ou /COFFRE=donnees), écrit dans installation.json à côté de l'exécutable ;
; - mise à jour sur place, avec le dossier, les raccourcis et l'emplacement des fiches choisis
;   la première fois ;
; - une version plus récente déjà installée n'est jamais remplacée : rien n'est modifié, et le
;   code de sortie est 7 ;
; - kyfRev ouvert est détecté avant l'installation comme avant la désinstallation (AppMutex) ;
; - la licence de kyfRev, les avis de tiers, les avis des paquets NuGet et les licences des polices
;   sont installés dans {app}\licences, et retirés avec l'application ;
; - la désinstallation ne touche jamais aux coffres, aux sauvegardes ni aux réglages des profils.

#ifndef Version
  #error Version manquante : /DVersion=<version SemVer>.
#endif
#ifndef NumericVersion
  #error Version numérique manquante : /DNumericVersion=<a.b.c.d>.
#endif
#ifndef Source
  #error Dossier de l'application publiée manquant : /DSource=<dossier>.
#endif

; Identité de l'application pour Windows : ne change jamais.
#define AppGuid "69A07F65-7110-4520-A128-737A61015928"

#ifdef ReleaseDate
  #define Published "Version " + Version + " publiée le " + ReleaseDate + "."
#else
  #define Published "Version " + Version + ", non publiée."
#endif

[Setup]
AppId={{{#AppGuid}}
AppName=kyfRev
AppVersion={#Version}
AppVerName=kyfRev {#Version}
AppPublisher=KyllianFF
AppCopyright=Copyright © 2026 KyllianFF
VersionInfoVersion={#NumericVersion}
VersionInfoTextVersion={#Version}
VersionInfoProductName=kyfRev
VersionInfoProductVersion={#NumericVersion}
VersionInfoProductTextVersion={#Version}
VersionInfoCompany=KyllianFF
VersionInfoCopyright=Copyright © 2026 KyllianFF
VersionInfoDescription=Installation de kyfRev
DefaultDirName={autopf}\kyfRev
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern dynamic
; Générées depuis assets/icon/kyfrev.svg par tools/icon/icon.cs ; Setup prend celles de l'échelle d'affichage.
SetupIconFile=..\assets\icon\kyfrev.ico
WizardImageFile=images\assistant-*.png
WizardSmallImageFile=images\petit-*.png
ShowLanguageDialog=no
AppMutex=kyfRev,Global\kyfRev
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=kyfRev
UninstallDisplayIcon={app}\kyfRev.exe
OutputDir=Output
OutputBaseFilename=kyfRev-{#Version}-installation
Compression=lzma2/max
SolidCompression=yes
#ifdef SignTool
SignTool={#SignTool}
SignedUninstaller=yes
#endif

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Messages]
WelcomeLabel2=kyfRev organise vos fiches de révision selon la méthode des J.%n%n{#Published}%n%nChaque personne garde ses fiches, ses sauvegardes et ses réglages dans son propre profil.
ConfirmUninstall=Voulez-vous retirer %1 de cet ordinateur ?%n%nLes fiches, les sauvegardes et les réglages de chaque personne restent dans son profil : rien n'est effacé.

[Files]
; Les polices sont dans les assemblys : Fonts ne contient que leurs licences, installées plus bas.
; La publication contient aussi licences\THIRD-PARTY-NOTICES-nuget.md, écrit par tools/installer.ps1.
Source: "{#Source}\*"; Excludes: "*.pdb,\Fonts"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Emplacement des fiches de chaque personne, lu au démarrage (mode installé).
Source: "installation-documents.json"; DestDir: "{app}"; DestName: "installation.json"; Check: VaultIn('documents'); Flags: ignoreversion
Source: "installation-donnees.json"; DestDir: "{app}"; DestName: "installation.json"; Check: VaultIn('donnees'); Flags: ignoreversion
; Licence de kyfRev, avis de tiers (deux fichiers du même nom dans le dépôt) et licences des polices.
Source: "..\LICENSE"; DestDir: "{app}\licences"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\src\KyfRev.Rendering\THIRD-PARTY-NOTICES.md"; DestDir: "{app}\licences"; DestName: "THIRD-PARTY-NOTICES-rendu.md"; Flags: ignoreversion
Source: "..\src\KyfRev.Presentation.Wpf\Editing\THIRD-PARTY-NOTICES.md"; DestDir: "{app}\licences"; DestName: "THIRD-PARTY-NOTICES-editeur.md"; Flags: ignoreversion
Source: "..\src\KyfRev.DesignSystem\Fonts\OFL-*.txt"; DestDir: "{app}\licences"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\kyfRev"; Filename: "{app}\kyfRev.exe"; Check: ShortcutWanted(0)
Name: "{autodesktop}\kyfRev"; Filename: "{app}\kyfRev.exe"; Check: ShortcutWanted(1)

[Run]
Filename: "{app}\kyfRev.exe"; Description: "{cm:LaunchProgram,kyfRev}"; Flags: nowait postinstall skipifsilent

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + '{#AppGuid}' + '}_is1';

var
  { Raccourcis (menu Démarrer, Bureau) et emplacement des fiches : pages de l'assistant, gardées
    d'une installation à la suivante. }
  ShortcutsPage: TInputOptionWizardPage;
  VaultPage: TInputOptionWizardPage;
  { Version la plus récente déjà installée, pour l'utilisateur en cours ou pour tous ; vide sinon. }
  InstalledVersion: String;
  NewerInstalled: Boolean;

{ Vrai si la ligne de commande contient /NOICONS : aucun raccourci. }
function NoIcons: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/NOICONS') = 0 then
      Result := True;
end;

{ Vrai pour une mise à jour : kyfRev est déjà installé dans le mode choisi (utilisateur ou tous). }
function IsUpdate: Boolean;
begin
  Result := RegValueExists(HKA, UninstallKey, 'DisplayVersion');
end;

function Sign(Value: Integer): Integer;
begin
  if Value < 0 then
    Result := -1
  else if Value > 0 then
    Result := 1
  else
    Result := 0;
end;

function IsDigits(const S: String): Boolean;
var
  I: Integer;
begin
  Result := Length(S) > 0;
  for I := 1 to Length(S) do
    if (S[I] < '0') or (S[I] > '9') then
      Result := False;
end;

{ Ordre de deux identifiants SemVer : les numériques par valeur (sans zéro initial, la longueur
  départage d'abord), les autres dans l'ordre ASCII ; un numérique précède un alphanumérique. }
function CompareIdentifier(const A, B: String): Integer;
begin
  if IsDigits(A) and IsDigits(B) then
  begin
    if Length(A) <> Length(B) then
      Result := Sign(Length(A) - Length(B))
    else
      Result := Sign(CompareStr(A, B));
  end
  else if IsDigits(A) then
    Result := -1
  else if IsDigits(B) then
    Result := 1
  else
    Result := Sign(CompareStr(A, B));
end;

{ Ordre de deux suites d'identifiants séparés par des points ; à égalité, la plus courte d'abord. }
function CompareIdentifiers(const A, B: String): Integer;
var
  PartsA, PartsB: TArrayOfString;
  I: Integer;
begin
  PartsA := StringSplit(A, ['.'], stAll);
  PartsB := StringSplit(B, ['.'], stAll);
  Result := 0;
  I := 0;
  while (Result = 0) and (I < GetArrayLength(PartsA)) and (I < GetArrayLength(PartsB)) do
  begin
    Result := CompareIdentifier(PartsA[I], PartsB[I]);
    I := I + 1;
  end;
  if Result = 0 then
    Result := Sign(GetArrayLength(PartsA) - GetArrayLength(PartsB));
end;

{ Précédence SemVer 2.0.0 de A par rapport à B : -1, 0 ou 1. Les métadonnées de compilation
  (« +… ») ne comptent pas ; une préversion (« -alpha.1 ») précède la version publiée. }
function CompareVersions(A, B: String): Integer;
var
  PreA, PreB: String;
  P: Integer;
begin
  P := Pos('+', A);
  if P > 0 then
    A := Copy(A, 1, P - 1);
  P := Pos('+', B);
  if P > 0 then
    B := Copy(B, 1, P - 1);
  PreA := '';
  P := Pos('-', A);
  if P > 0 then
  begin
    PreA := Copy(A, P + 1, Length(A));
    A := Copy(A, 1, P - 1);
  end;
  PreB := '';
  P := Pos('-', B);
  if P > 0 then
  begin
    PreB := Copy(B, P + 1, Length(B));
    B := Copy(B, 1, P - 1);
  end;
  Result := CompareIdentifiers(A, B);
  if Result = 0 then
  begin
    if (PreA = '') and (PreB <> '') then
      Result := 1
    else if (PreA <> '') and (PreB = '') then
      Result := -1
    else if PreA <> '' then
      Result := CompareIdentifiers(PreA, PreB);
  end;
end;

{ La plus récente des deux versions, l'une ou l'autre pouvant être vide. }
function Newest(const A, B: String): String;
begin
  if (A = '') or ((B <> '') and (CompareVersions(B, A) > 0)) then
    Result := B
  else
    Result := A;
end;

function InitializeSetup: Boolean;
var
  ForUser, ForAll: String;
begin
  if not RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', ForUser) then
    ForUser := '';
  if not RegQueryStringValue(HKLM, UninstallKey, 'DisplayVersion', ForAll) then
    ForAll := '';
  InstalledVersion := Newest(ForUser, ForAll);
  NewerInstalled := (InstalledVersion <> '') and (CompareVersions(InstalledVersion, '{#Version}') > 0);
  Result := True;
end;

procedure InitializeWizard;
var
  Choice: String;
begin
  ShortcutsPage := CreateInputOptionPage(wpSelectDir,
    'Raccourcis',
    'D''où lancer kyfRev ?',
    'Cochez les raccourcis à créer.',
    False, False);
  ShortcutsPage.Add('Dans le menu Démarrer');
  ShortcutsPage.Add('Sur le Bureau');
  ShortcutsPage.Values[0] := not NoIcons and (GetPreviousData('MenuDemarrer', 'oui') = 'oui');
  ShortcutsPage.Values[1] := not NoIcons and (GetPreviousData('Bureau', 'oui') = 'oui');

  VaultPage := CreateInputOptionPage(ShortcutsPage.ID,
    'Fiches et sauvegardes',
    'Où chaque personne range-t-elle ses fiches ?',
    'Chaque personne qui utilise cet ordinateur aura son propre coffre : ses fiches, ses sauvegardes et ses réglages restent dans son profil.',
    True, False);
  VaultPage.Add('Dans « Documents\kyfRev » : faciles à retrouver et à sauvegarder (recommandé)');
  VaultPage.Add('Dans les données d''application (AppData), hors de vue');
  { Le choix de la ligne de commande, sinon celui de l'installation précédente. }
  Choice := Lowercase(ExpandConstant('{param:COFFRE|}'));
  if (Choice <> 'documents') and (Choice <> 'donnees') then
    Choice := GetPreviousData('Coffre', 'documents');
  if Choice = 'donnees' then
    VaultPage.SelectedValueIndex := 1
  else
    VaultPage.SelectedValueIndex := 0;
end;

{ Oui ou non, comme le gardent les données de l'installation. }
function YesNo(Value: Boolean): String;
begin
  if Value then
    Result := 'oui'
  else
    Result := 'non';
end;

{ Raccourci coché : 0 pour le menu Démarrer, 1 pour le Bureau. }
function ShortcutWanted(Index: Integer): Boolean;
begin
  Result := ShortcutsPage.Values[Index];
end;

{ Rangement choisi : « documents » ou « donnees ». }
function VaultChoice: String;
begin
  if VaultPage.SelectedValueIndex = 1 then
    Result := 'donnees'
  else
    Result := 'documents';
end;

function VaultIn(const Choice: String): Boolean;
begin
  Result := VaultChoice = Choice;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'MenuDemarrer', YesNo(ShortcutWanted(0)));
  SetPreviousData(PreviousDataKey, 'Bureau', YesNo(ShortcutWanted(1)));
  SetPreviousData(PreviousDataKey, 'Coffre', VaultChoice);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  if NewerInstalled then
    { Directement à l'étape qui annonce que rien ne sera modifié. }
    Result := True
  else if PageID = ShortcutsPage.ID then
    Result := NoIcons or IsUpdate
  else if PageID = VaultPage.ID then
    Result := IsUpdate
  else
    Result := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if NewerInstalled then
    Result := FmtMessage('Une version plus récente de kyfRev (%1) est déjà installée sur cet ordinateur. Cette installation, en version %2, ne la remplace pas : rien n''est modifié.', [InstalledVersion, '{#Version}'])
  else
    Result := '';
end;

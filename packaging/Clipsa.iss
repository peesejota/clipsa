; Compile with Inno Setup 6.3+ and the complete self-contained installed payload.
#ifndef AppVersion
  #define AppVersion "0.4.3"
#endif
#ifndef PayloadDir
  #error PayloadDir must point to a self-contained publish directory
#endif
#ifndef ReleaseDir
  #error ReleaseDir must point to the output directory
#endif
#ifndef RepoDir
  #define RepoDir ".."
#endif
#ifndef AppIdOverride
  #define AppIdOverride "{{CA0981CC-559D-44B4-B3B9-C4B44C2F2830}"
#endif
#ifndef AppNameOverride
  #define AppNameOverride "Clipsa"
#endif

[Setup]
AppId={#AppIdOverride}
AppName={#AppNameOverride}
AppVersion={#AppVersion}
AppVerName={#AppNameOverride} {#AppVersion} beta
AppPublisher=Francisco Serrano Jiménez
AppCopyright=Copyright (c) 2026 Francisco Serrano Jiménez
DefaultDirName={localappdata}\Programs\{#AppNameOverride}
DefaultGroupName={#AppNameOverride}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19045
AppMutex=Local\Clipsa.Paco.Windows.v1
SetupMutex=Local\{#AppNameOverride}.Installer
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\Clipsa.exe
SetupIconFile={#RepoDir}\Clipsa\Brand\clipsa-icon.ico
LicenseFile={#RepoDir}\LICENSE
OutputDir={#ReleaseDir}
OutputBaseFilename=Clipsa-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
Uninstallable=yes
#ifdef EnableSigning
SignTool=clipsa
SignedUninstaller=yes
#else
SignedUninstaller=no
#endif

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "portable.mode,data\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoDir}\licenses\tendedero-LICENSE.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppNameOverride}"; Filename: "{app}\Clipsa.exe"; WorkingDir: "{app}"
Name: "{group}\Desinstalar {#AppNameOverride}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppNameOverride}"; Filename: "{app}\Clipsa.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Clipsa.exe"; Description: "Abrir {#AppNameOverride}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command, OwnExe: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    OwnExe := ExpandConstant('{app}\Clipsa.exe');
    if RegQueryStringValue(HKCU, RunKey, 'Clipsa', Command) then
    begin
      Command := Trim(Command);
      { Exact full path only; no substring/name matching and no wildcard deletion.
        Do not delete values pointing to another installed/portable copy. }
      if (CompareText(Command, '"' + OwnExe + '" --background') = 0) or
         (CompareText(Command, '"' + OwnExe + '"') = 0) or
         (CompareText(Command, OwnExe) = 0) then
        RegDeleteValue(HKCU, RunKey, 'Clipsa');
    end;
  end;
end;

{ No Registry autorun creation, UninstallDelete, profile/data deletion or runtime installer. }

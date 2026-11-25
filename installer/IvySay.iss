; Build a self-contained app first (see README), then compile with Inno Setup 6.
#define AppVersion "0.1.0"
[Setup]
AppId={{A195A145-C0AC-4978-A216-4955E98DD021}
AppName=Ivy Say
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Ivy Say
DefaultGroupName=Ivy Say
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=IvySay-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\Ivy Say"; Filename: "{app}\IvySay.exe"
[Run]
Filename: "{app}\IvySay.exe"; Description: "Launch Ivy Say"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Current: String;
  Expected: String;
begin
  if CurUninstallStep = usUninstall then begin
    Expected := '"' + ExpandConstant('{app}\IvySay.exe') + '" --startup';
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'IvySay', Current)
       and (CompareText(Current, Expected) = 0) then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'IvySay');
  end;
end;

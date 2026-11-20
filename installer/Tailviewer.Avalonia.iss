#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif
#define Payload "..\artifacts\publish\tailviewer-" + AppVersion + "-win-x64"

[Setup]
AppId={{C2E9EFDA-3CF4-4B26-9D41-1D8AE47F0372}
AppName=Tailviewer
AppVersion={#AppVersion}
AppPublisher=Tailviewer contributors
AppPublisherURL=https://github.com/sannlynnhtun-coding/tailviewer
DefaultDirName={localappdata}\Programs\Tailviewer
DefaultGroupName=Tailviewer
UsePreviousGroup=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ChangesAssociations=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Tailviewer.Avalonia.exe
SetupIconFile=..\src\Tailviewer\Resources\Tailviewer.ico
LicenseFile=..\LICENSE
OutputDir=..\artifacts
OutputBaseFilename=tailviewer-{#AppVersion}-win-x64-setup
Compression=lzma2/fast
SolidCompression=yes

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Tailviewer"; Filename: "{app}\Tailviewer.Avalonia.exe"

[Registry]
Root: HKCU; Subkey: "Software\Classes\Applications\Tailviewer.Avalonia.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Tailviewer"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\Tailviewer.Avalonia.exe\shell\open\command"; ValueType: string; ValueData: """{app}\Tailviewer.Avalonia.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\Tailviewer.Avalonia.exe\SupportedTypes"; ValueType: string; ValueName: ".log"
Root: HKCU; Subkey: "Software\Classes\Applications\Tailviewer.Avalonia.exe\SupportedTypes"; ValueType: string; ValueName: ".txt"
Root: HKCU; Subkey: "Software\Classes\Tailviewer.LogFile"; ValueType: string; ValueData: "Tailviewer"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Tailviewer.LogFile\shell\open\command"; ValueType: string; ValueData: """{app}\Tailviewer.Avalonia.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.log\OpenWithProgids"; ValueType: string; ValueName: "Tailviewer.LogFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.txt\OpenWithProgids"; ValueType: string; ValueName: "Tailviewer.LogFile"; Flags: uninsdeletevalue

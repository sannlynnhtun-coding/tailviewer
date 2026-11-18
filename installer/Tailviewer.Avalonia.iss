#ifndef AppVersion
  #define AppVersion "2.0.0-alpha.2"
#endif
#define Payload "..\artifacts\publish\tailviewer-" + AppVersion + "-win-x64"

[Setup]
AppId={{C2E9EFDA-3CF4-4B26-9D41-1D8AE47F0372}
AppName=Tailviewer Avalonia Preview
AppVersion={#AppVersion}
AppPublisher=Tailviewer contributors
AppPublisherURL=https://github.com/sannlynnhtun-coding/tailviewer
DefaultDirName={localappdata}\Programs\Tailviewer Avalonia Preview
DefaultGroupName=Tailviewer Avalonia Preview
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
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
Name: "{group}\Tailviewer Avalonia Preview"; Filename: "{app}\Tailviewer.Avalonia.exe"

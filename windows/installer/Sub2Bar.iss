#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define AppName "Sub2Bar"
#define AppPublisher "Sub2Bar"
#define AppExeName "Sub2Bar.exe"
#define RepositoryRoot ".."

[Setup]
AppId={{3A9871C6-BB5B-4C31-A69C-C86D5261A318}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Sub2Bar
DefaultGroupName=Sub2Bar
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#RepositoryRoot}\artifacts
OutputBaseFilename=Sub2Bar-{#AppVersion}-win-x64-setup
SetupIconFile={#RepositoryRoot}\src\Sub2Bar.Windows\Assets\Sub2Bar.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0.19045
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
RestartApplications=no
AppMutex=Local\com.sub2bar.windows
ChangesAssociations=no

[Languages]
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式"; Flags: unchecked

[Files]
Source: "{#RepositoryRoot}\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepositoryRoot}\artifacts\installer\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Sub2Bar"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Sub2Bar"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Sub2Bar"; Flags: uninsdeletevalue

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "正在安装 Microsoft Edge WebView2 Runtime..."; Flags: waituntilterminated; Check: not IsWebView2RuntimeInstalled
Filename: "{app}\{#AppExeName}"; Description: "启动 Sub2Bar"; Flags: nowait postinstall skipifsilent

[Code]
var
  RemovePersonalData: Boolean;

function RuntimeVersionExists(const Root: Integer; const Key: String): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(Root, Key, 'pv', Version) and
            (Version <> '') and (Version <> '0.0.0.0');
end;

function IsWebView2RuntimeInstalled: Boolean;
var
  ClientKey: String;
begin
  ClientKey := 'Software\Microsoft\EdgeUpdate\Clients\{F1E7EADC-6FBE-43F2-B9D9-2B0A16D7817E}';
  Result := RuntimeVersionExists(HKEY_CURRENT_USER, ClientKey) or
            RuntimeVersionExists(HKEY_LOCAL_MACHINE, ClientKey) or
            RuntimeVersionExists(HKEY_LOCAL_MACHINE, 'Software\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F1E7EADC-6FBE-43F2-B9D9-2B0A16D7817E}');
end;

function InitializeUninstall: Boolean;
begin
  RemovePersonalData := MsgBox('是否同时删除 Sub2Bar 的本地设置和登录凭据？',
    mbConfirmation, MB_YESNO) = IDYES;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and RemovePersonalData then
    DelTree(ExpandConstant('{localappdata}\Sub2Bar'), True, True, True);
end;

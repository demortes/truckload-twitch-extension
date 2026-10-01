; Truckload Bridge installer (Inno Setup 6).
;
; Installs the Truckload Bridge and the TruckTel game plugin, finds ATS / ETS2 on Steam, and
; stores the streamer's ingest key so the Bridge is one double-click from working.
; Build with installer\build.ps1 (it stages the files this script expects).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef DefaultIngestUrl
  #define DefaultIngestUrl "https://truckload.demortes.com/api/ingest"
#endif

[Setup]
AppId={{C2032BD8-8F7B-487C-8A85-B28C56C6B97C}
AppName=Truckload Bridge
AppVersion={#AppVersion}
AppPublisher=demortes
AppPublisherURL=https://github.com/demortes/truckload-twitch-extension
AppSupportURL=https://github.com/demortes/truckload-twitch-extension/issues
DefaultDirName={localappdata}\Programs\Truckload Bridge
DefaultGroupName=Truckload
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=TruckloadSetup-v{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Truckload.Bridge.exe
CloseApplications=yes

[Types]
Name: "custom"; Description: "Choose which games to set up"; Flags: iscustom

[Components]
Name: "ats"; Description: "American Truck Simulator (install the telemetry plugin)"; Types: custom
Name: "ets2"; Description: "Euro Truck Simulator 2 (install the telemetry plugin)"; Types: custom

[Messages]
WizardSelectComponents=Your games
SelectComponentsDesc=Which games do you play?
SelectComponentsLabel2=Tick each game you want Truckload to work with. Games found on Steam are ticked for you. You can pick just one, and you can run this installer again later to add the other.

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "staging\Truckload.Bridge.exe"; DestDir: "{app}"; Flags: ignoreversion
; TruckTel game plugin (MIT) is copied into each selected game's plugins folder.
Source: "staging\trucktel.dll"; DestDir: "{code:AtsPluginDir}"; Flags: ignoreversion; Components: ats
Source: "staging\trucktel\LICENSE"; DestDir: "{code:AtsPluginDir}\trucktel"; Flags: ignoreversion; Components: ats
Source: "staging\trucktel.dll"; DestDir: "{code:Ets2PluginDir}"; Flags: ignoreversion; Components: ets2
Source: "staging\trucktel\LICENSE"; DestDir: "{code:Ets2PluginDir}\trucktel"; Flags: ignoreversion; Components: ets2

[Icons]
Name: "{group}\Truckload Bridge"; Filename: "{app}\Truckload.Bridge.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall Truckload"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Truckload Bridge"; Filename: "{app}\Truckload.Bridge.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Truckload.Bridge.exe"; WorkingDir: "{app}"; Description: "Start the Truckload Bridge now"; Flags: postinstall nowait skipifsilent unchecked

[Code]
var
  GamesPage: TInputDirWizardPage;
  KeyPage: TInputQueryWizardPage;

// ---------------------------------------------------------------- Steam game detection

function ReadVdfPathValue(const Line: String): String;
var
  P, Q: Integer;
  Rest: String;
begin
  Result := '';
  P := Pos('"path"', Line);
  if P = 0 then Exit;
  Rest := Copy(Line, P + 6, Length(Line));
  P := Pos('"', Rest);
  if P = 0 then Exit;
  Rest := Copy(Rest, P + 1, Length(Rest));
  Q := Pos('"', Rest);
  if Q = 0 then Exit;
  Result := Copy(Rest, 1, Q - 1);
  StringChangeEx(Result, '\\', '\', True);
end;

function TryLibrary(const Lib, GameFolder: String; var Root: String): Boolean;
begin
  Root := Lib + '\steamapps\common\' + GameFolder;
  Result := DirExists(Root + '\bin\win_x64');
  if not Result then Root := '';
end;

// Returns the game's install folder (the one containing bin\win_x64) or ''.
function FindGameRoot(const GameFolder: String): String;
var
  Steam, Lib: String;
  Lines: TArrayOfString;
  I: Integer;
begin
  Result := '';
  if not RegQueryStringValue(HKCU, 'Software\Valve\Steam', 'SteamPath', Steam) then Exit;
  StringChangeEx(Steam, '/', '\', True);
  if TryLibrary(Steam, GameFolder, Result) then Exit;
  if LoadStringsFromFile(Steam + '\steamapps\libraryfolders.vdf', Lines) then
  begin
    for I := 0 to GetArrayLength(Lines) - 1 do
    begin
      Lib := ReadVdfPathValue(Lines[I]);
      if (Lib <> '') and TryLibrary(Lib, GameFolder, Result) then Exit;
    end;
  end;
end;

// ---------------------------------------------------------------- helpers used by [Files]

function PluginDirFor(const Root: String): String;
begin
  Result := RemoveBackslashUnlessRoot(Trim(Root)) + '\bin\win_x64\plugins';
end;

function AtsPluginDir(Param: String): String;
begin
  Result := PluginDirFor(GamesPage.Values[0]);
end;

function Ets2PluginDir(Param: String): String;
begin
  Result := PluginDirFor(GamesPage.Values[1]);
end;

function InstallAts: Boolean;
begin
  Result := WizardIsComponentSelected('ats') and (Trim(GamesPage.Values[0]) <> '');
end;

function InstallEts2: Boolean;
begin
  Result := WizardIsComponentSelected('ets2') and (Trim(GamesPage.Values[1]) <> '');
end;

function JsonEscape(const S: String): String;
begin
  Result := S;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
end;

// ---------------------------------------------------------------- wizard pages

procedure InitializeWizard;
begin
  GamesPage := CreateInputDirPage(wpSelectComponents,
    'Game folders',
    'Where are your games installed?',
    'Truckload installs its telemetry plugin (TruckTel) into the game folders below. ' +
    'Folders were found automatically where possible. Only the games you ticked on the previous page are used.',
    False, '');
  GamesPage.Add('American Truck Simulator folder:');
  GamesPage.Add('Euro Truck Simulator 2 folder:');
  GamesPage.Values[0] := FindGameRoot('American Truck Simulator');
  GamesPage.Values[1] := FindGameRoot('Euro Truck Simulator 2');

  // Tick a game only if it was found on this PC; the other stays optional.
  // (For silent installs, an explicit /COMPONENTS= on the command line decides.)
  if not WizardSilent then
  begin
    if GamesPage.Values[0] <> '' then WizardSelectComponents('ats') else WizardSelectComponents('!ats');
    if GamesPage.Values[1] <> '' then WizardSelectComponents('ets2') else WizardSelectComponents('!ets2');
  end;

  KeyPage := CreateInputQueryPage(GamesPage.ID,
    'Connect to your channel',
    'Paste your Truckload ingest key',
    'On Twitch, open your Creator Dashboard, then the Truckload extension''s Config page, and ' +
    'click Generate API Key. Paste the key below. Leave it blank to keep an existing key or to set it up later.');
  KeyPage.Add('Ingest key:', False);
  KeyPage.Add('Backend ingest URL (leave as is unless you were told otherwise):', False);
  KeyPage.Values[0] := ExpandConstant('{param:KEY|}');
  KeyPage.Values[1] := '{#DefaultIngestUrl}';
end;

function IsWritableDir(const Dir: String): Boolean;
var
  TestFile: String;
begin
  TestFile := Dir + '\.truckload-write-test';
  Result := SaveStringToFile(TestFile, 'x', False);
  if Result then DeleteFile(TestFile);
end;

function ValidateGameRoot(const Name, Root: String): Boolean;
var
  R: String;
begin
  Result := True;
  R := Trim(Root);
  if R = '' then Exit;
  if not DirExists(R + '\bin\win_x64') then
  begin
    MsgBox(Name + ': "' + R + '" does not look like a game folder (it has no bin\win_x64 inside). ' +
      'In Steam, right-click the game, choose Manage, then Browse local files, and use that folder. ' +
      'Or go Back and untick this game.', mbError, MB_OK);
    Result := False;
    Exit;
  end;
  if not IsWritableDir(R + '\bin\win_x64') then
  begin
    MsgBox(Name + ': Setup cannot write to "' + R + '\bin\win_x64". Close the installer and run it again ' +
      'as administrator (right-click, Run as administrator), or go Back and untick this game.', mbError, MB_OK);
    Result := False;
  end;
end;

// The folder page is only relevant when at least one game was ticked.
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = GamesPage.ID then
    Result := not (WizardIsComponentSelected('ats') or WizardIsComponentSelected('ets2'));
end;

// Only the ticked games' folder boxes are editable.
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = GamesPage.ID then
  begin
    GamesPage.Edits[0].Enabled := WizardIsComponentSelected('ats');
    GamesPage.Buttons[0].Enabled := WizardIsComponentSelected('ats');
    GamesPage.Edits[1].Enabled := WizardIsComponentSelected('ets2');
    GamesPage.Buttons[1].Enabled := WizardIsComponentSelected('ets2');
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = wpSelectComponents then
  begin
    if not (WizardIsComponentSelected('ats') or WizardIsComponentSelected('ets2')) then
      Result := MsgBox('No game is ticked, so the telemetry plugin will not be installed and the ' +
        'Bridge will not get any data until you install TruckTel yourself. Continue anyway?',
        mbConfirmation, MB_YESNO) = IDYES;
  end;

  if CurPageID = GamesPage.ID then
  begin
    if WizardIsComponentSelected('ats') then
    begin
      if Trim(GamesPage.Values[0]) = '' then
      begin
        MsgBox('American Truck Simulator is ticked but no folder was given. Enter its folder, or go Back ' +
          'and untick it.', mbError, MB_OK);
        Result := False;
        Exit;
      end;
      Result := ValidateGameRoot('American Truck Simulator', GamesPage.Values[0]);
      if not Result then Exit;
    end;
    if WizardIsComponentSelected('ets2') then
    begin
      if Trim(GamesPage.Values[1]) = '' then
      begin
        MsgBox('Euro Truck Simulator 2 is ticked but no folder was given. Enter its folder, or go Back ' +
          'and untick it.', mbError, MB_OK);
        Result := False;
        Exit;
      end;
      Result := ValidateGameRoot('Euro Truck Simulator 2', GamesPage.Values[1]);
    end;
  end;
end;

// ---------------------------------------------------------------- install / uninstall

procedure WriteBridgeConfig;
var
  ConfigPath, Key, Url, Json: String;
begin
  ConfigPath := ExpandConstant('{app}\truckload-bridge.json');
  Key := Trim(KeyPage.Values[0]);
  Url := Trim(KeyPage.Values[1]);
  if Key = '' then Exit; // keep whatever config already exists
  if Url = '' then Url := '{#DefaultIngestUrl}';

  Json := '{' + #13#10 +
    '  "IngestUrl": "' + JsonEscape(Url) + '",' + #13#10 +
    '  "IngestKey": "' + JsonEscape(Key) + '",' + #13#10 +
    '  "Source": "trucktel"' + #13#10 +
    '}' + #13#10;
  SaveStringToFile(ConfigPath, Json, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    WriteBridgeConfig;
    // Remember where the plugin went so the uninstaller can remove it.
    if InstallAts then
      RegWriteStringValue(HKCU, 'Software\Truckload', 'AtsPluginDir', AtsPluginDir(''));
    if InstallEts2 then
      RegWriteStringValue(HKCU, 'Software\Truckload', 'Ets2PluginDir', Ets2PluginDir(''));
  end;
end;

procedure RemovePlugin(const ValueName: String);
var
  Dir: String;
begin
  if RegQueryStringValue(HKCU, 'Software\Truckload', ValueName, Dir) and (Dir <> '') then
  begin
    DeleteFile(Dir + '\trucktel.dll');
    DeleteFile(Dir + '\trucktel\LICENSE');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemovePlugin('AtsPluginDir');
    RemovePlugin('Ets2PluginDir');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Truckload');
  end;
end;

#define AppName "AIther"
#define AppVersion "1.0"
#define AppPublisher "AIther"

[Setup]
; Stable GUID — keeps Windows from treating every build as a new unknown app
AppId={{A3F2C1D4-8E5B-4F6A-9C2D-1B3E7F8A0D5C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://aither-uo.com
AppSupportURL=https://discord.gg/ageHTmEr8
AppUpdatesURL=https://aither-uo.com

; Version info shown in Windows Explorer / Add-Remove Programs
VersionInfoDescription=AIther UO Launcher Installer
VersionInfoProductName=AIther
VersionInfoCompanyName=AIther
VersionInfoVersion=1.0.0.0

; Install to user's local appdata — no admin elevation needed
DefaultDirName={localappdata}\AIther
DefaultGroupName=AIther
OutputBaseFilename=AIther-Setup
OutputDir=C:\UO\servuo\website\installer_output
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; No admin = no UAC popup
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline

DisableProgramGroupPage=yes
UninstallDisplayName=AIther

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "sqlite3.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\AIther"; Filename: "{app}\ClassicUOLauncher.exe"
Name: "{commondesktop}\AIther"; Filename: "{app}\ClassicUOLauncher.exe"

[Run]
Filename: "{app}\ClassicUOLauncher.exe"; Description: "Launch AIther now"; Flags: postinstall nowait skipifsilent

[Code]

var
  UOPathPage: TInputDirWizardPage;

// ── Wizard pages ─────────────────────────────────────────────────────────────

procedure InitializeWizard;
begin
  UOPathPage := CreateInputDirPage(
    wpSelectDir,
    'Ultima Online Client Path',
    'Where is your Ultima Online client installed?',
    'Select the folder that contains uo.exe (the patched UO Classic client).',
    False, '');
  UOPathPage.Add('');
  // Try the most common install locations in order
  if DirExists(ExpandConstant('{commonpf32}\Electronic Arts\Ultima Online Classic')) then
    UOPathPage.Values[0] := ExpandConstant('{commonpf32}\Electronic Arts\Ultima Online Classic')
  else if DirExists(ExpandConstant('{commonpf}\Electronic Arts\Ultima Online Classic')) then
    UOPathPage.Values[0] := ExpandConstant('{commonpf}\Electronic Arts\Ultima Online Classic')
  else
    UOPathPage.Values[0] := ExpandConstant('{commonpf32}\Electronic Arts\Ultima Online Classic');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = UOPathPage.ID then begin
    if not FileExists(UOPathPage.Values[0] + '\uo.exe') and
       not FileExists(UOPathPage.Values[0] + '\client.exe') then begin
      MsgBox('Could not find uo.exe in that folder. Please select the folder where you installed the UO Classic client.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// ── Helpers ──────────────────────────────────────────────────────────────────

procedure SetStatus(Msg: String);
begin
  WizardForm.StatusLabel.Caption := Msg;
  WizardForm.StatusLabel.Update;
end;

procedure RunPS(Script: String);
var
  Code: Integer;
begin
  Exec('powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -Command "' + Script + '"',
    '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

procedure RunSQLite(DB, SQL: String);
var
  Code: Integer;
  SqlitePath: String;
begin
  SqlitePath := ExpandConstant('{tmp}') + '\sqlite3.exe';
  Exec(SqlitePath, '"' + DB + '" "' + SQL + '"', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

// ── Main install logic ────────────────────────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
var
  InstallDir, UOPath, PluginsDir, LauncherDB, SqlitePath, PluginPath, TmpPath: String;
  Lines: TArrayOfString;
begin
  if CurStep <> ssInstall then Exit;

  InstallDir := WizardDirValue;
  UOPath     := UOPathPage.Values[0];
  // ClassicUO launcher extracts its own files flat into InstallDir
  // Plugins go into a Plugins subfolder next to the launcher
  PluginsDir := InstallDir + '\Plugins';
  LauncherDB := ExpandConstant('{userappdata}') + '\ClassicUOLauncher\launcher.db';
  SqlitePath := ExpandConstant('{tmp}') + '\sqlite3.exe';
  TmpPath    := ExpandConstant('{tmp}');

  // ── Step 1: Download ClassicUO Launcher ───────────────────────────────────
  SetStatus('Downloading ClassicUO Launcher...');
  RunPS(
    '$url = "https://github.com/ClassicUO/deploy/releases/latest/download/ClassicUOLauncher-win-x64-release.zip";' +
    'Invoke-WebRequest -Uri $url -OutFile "$env:TEMP\cuo.zip" -UseBasicParsing;' +
    '$dest = "' + InstallDir + '";' +
    'Expand-Archive -Path "$env:TEMP\cuo.zip" -DestinationPath $dest -Force;' +
    // If the zip extracted into a single subfolder, move contents up one level
    '$items = Get-ChildItem -Path $dest;' +
    'if ($items.Count -eq 1 -and $items[0].PSIsContainer) {' +
    '  $sub = $items[0].FullName;' +
    '  Get-ChildItem -Path $sub | Move-Item -Destination $dest -Force;' +
    '  Remove-Item $sub -Force -Recurse;' +
    '};' +
    'Remove-Item "$env:TEMP\cuo.zip" -Force'
  );

  // ── Step 2: Download Razor Enhanced ───────────────────────────────────────
  SetStatus('Downloading Razor Enhanced...');
  RunPS(
    'New-Item -ItemType Directory -Path "' + PluginsDir + '" -Force | Out-Null;' +
    '$r = Invoke-RestMethod -Uri "https://api.github.com/repos/RazorEnhanced/RazorEnhanced/releases/latest";' +
    '$a = $r.assets | Where-Object { $_.name -like "RazorEnhanced-*.zip" } | Select-Object -First 1;' +
    'Invoke-WebRequest -Uri $a.browser_download_url -OutFile "$env:TEMP\razor.zip" -UseBasicParsing;' +
    'Expand-Archive -Path "$env:TEMP\razor.zip" -DestinationPath "' + PluginsDir + '" -Force;' +
    'Remove-Item "$env:TEMP\razor.zip" -Force;' +
    '$exe = (Get-ChildItem -Path "' + PluginsDir + '" -Filter "RazorEnhanced.exe" -Recurse | Select-Object -First 1).FullName;' +
    'if ($exe) { $exe | Set-Content -Path "' + TmpPath + '\razorpath.txt" -Encoding UTF8 }'
  );

  // Read actual plugin path discovered above
  PluginPath := PluginsDir + '\RazorEnhanced.exe';
  if LoadStringsFromFile(TmpPath + '\razorpath.txt', Lines) then
    if GetArrayLength(Lines) > 0 then
      PluginPath := Trim(Lines[0]);

  // ── Step 3: Configure launcher database ───────────────────────────────────
  SetStatus('Configuring AIther profile...');

  ForceDirectories(ExpandConstant('{userappdata}') + '\ClassicUOLauncher');

  // Schema
  RunSQLite(LauncherDB, 'CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);');
  RunSQLite(LauncherDB, 'CREATE TABLE IF NOT EXISTS profiles (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, sort_order INTEGER NOT NULL, cuo_path TEXT, username TEXT, password TEXT, server TEXT, port TEXT, charname TEXT, client_version TEXT, uo_path TEXT, server_type INTEGER DEFAULT 0, last_server_index INTEGER DEFAULT 0, last_server_name TEXT, debug INTEGER DEFAULT 0, profiler INTEGER DEFAULT 0, save_account INTEGER DEFAULT 0, skip_login_screen INTEGER DEFAULT 0, autologin INTEGER DEFAULT 0, reconnect INTEGER DEFAULT 0, reconnect_time INTEGER DEFAULT 1000, has_music INTEGER DEFAULT 1, high_dpi INTEGER DEFAULT 0, use_verdata INTEGER DEFAULT 0, uo_protocol INTEGER DEFAULT 0, music_volume INTEGER DEFAULT 50, encryption_type INTEGER DEFAULT 0, force_driver INTEGER DEFAULT 0, packet_log INTEGER DEFAULT 0, args TEXT DEFAULT '''');');
  RunSQLite(LauncherDB, 'CREATE TABLE IF NOT EXISTS profile_plugins (id INTEGER PRIMARY KEY AUTOINCREMENT, profile_id INTEGER NOT NULL, path TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1, FOREIGN KEY(profile_id) REFERENCES profiles(id) ON DELETE CASCADE);');
  RunSQLite(LauncherDB, 'CREATE TABLE IF NOT EXISTS web_server_history (server_id INTEGER PRIMARY KEY, last_played_at INTEGER NOT NULL);');

  // Remove any old AIther / Fun Stuff profiles and insert fresh one
  RunSQLite(LauncherDB, 'DELETE FROM profile_plugins WHERE profile_id IN (SELECT id FROM profiles WHERE name IN (''AIther'', ''Fun Stuff UO'', ''FunStuffUO''));');
  RunSQLite(LauncherDB, 'DELETE FROM profiles WHERE name IN (''AIther'', ''Fun Stuff UO'', ''FunStuffUO'');');
  RunSQLite(LauncherDB,
    'INSERT INTO profiles (name, sort_order, cuo_path, server, port, client_version, uo_path, encryption_type, last_server_name) ' +
    'VALUES (''AIther'', 0, ''' + InstallDir + ''', ''aither-uo.com'', ''2593'', ''7.0.115.0'', ''' + UOPath + ''', 0, ''AIther'');'
  );

  // Wire up Razor Enhanced plugin and select this profile
  RunPS(
    '$db = "' + LauncherDB + '";' +
    '$sq = "' + SqlitePath + '";' +
    '$id = (& $sq $db "SELECT id FROM profiles WHERE name = ''AIther'' ORDER BY id DESC LIMIT 1;").Trim();' +
    'if ($id) {' +
    '  & $sq $db "INSERT INTO profile_plugins (profile_id, path, enabled) VALUES ($id, ''' + PluginPath + ''', 1);";' +
    '  & $sq $db "INSERT OR REPLACE INTO settings (key, value) VALUES (''selected_profile_index'', ''0'');";' +
    '  & $sq $db "INSERT OR REPLACE INTO settings (key, value) VALUES (''last_profile_name'', ''AIther'');"' +
    '}'
  );

  SetStatus('Done!');
end;

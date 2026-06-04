#define AppName "AIther"
#define AppVersion "1.0"
#define AppPublisher "AIther"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={commonpf32}\AIther
DefaultGroupName=AIther
OutputBaseFilename=AIther-Setup
OutputDir=C:\UO\servuo\website\installer_output
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableProgramGroupPage=yes
UninstallDisplayName=AIther

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "sqlite3.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\AIther Launcher"; Filename: "{app}\ClassicUOLauncher.exe"
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
  UOPathPage.Values[0] := ExpandConstant('{commonpf32}\Electronic Arts\Ultima Online Classic');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = UOPathPage.ID then begin
    if not DirExists(UOPathPage.Values[0]) then begin
      MsgBox('That folder does not exist. Please check the path to your UO client.', mbError, MB_OK);
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

// Run a PowerShell script. Returns the exit code.
function RunPS(Script: String): Integer;
var
  Code: Integer;
begin
  Exec('powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -Command "' + Script + '"',
    '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := Code;
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
  InstallDir, UOPath, PluginsDir, CUOPath, LauncherDB, SqlitePath, PluginPath, TmpPath: String;
  Lines: TArrayOfString;
  Code: Integer;
begin
  if CurStep <> ssInstall then Exit;

  InstallDir := WizardDirValue;
  UOPath     := UOPathPage.Values[0];
  PluginsDir := InstallDir + '\Plugins';
  CUOPath    := InstallDir;
  LauncherDB := ExpandConstant('{userappdata}') + '\ClassicUOLauncher\launcher.db';
  SqlitePath := ExpandConstant('{tmp}') + '\sqlite3.exe';
  TmpPath    := ExpandConstant('{tmp}');

  // ── Step 1: Download ClassicUO Launcher ───────────────────────────────────
  SetStatus('Downloading ClassicUO Launcher (this may take a minute)...');
  Code := RunPS(
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12;' +
    'try {' +
    '  $url = "https://github.com/ClassicUO/deploy/releases/latest/download/ClassicUOLauncher-win-x64-release.zip";' +
    '  $dest = "' + InstallDir + '";' +
    '  $zip = "$env:TEMP\cuo.zip";' +
    '  $wc = New-Object System.Net.WebClient;' +
    '  $wc.DownloadFile($url, $zip);' +
    '  Expand-Archive -Path $zip -DestinationPath $dest -Force;' +
    '  Remove-Item $zip -Force;' +
    // If zip extracted into a single subfolder, flatten it
    '  $items = Get-ChildItem -Path $dest;' +
    '  if ($items.Count -eq 1 -and $items[0].PSIsContainer) {' +
    '    $sub = $items[0].FullName;' +
    '    Get-ChildItem -Path $sub | Move-Item -Destination $dest -Force;' +
    '    Remove-Item $sub -Force -Recurse;' +
    '  };' +
    '  exit 0' +
    '} catch { exit 1 }'
  );

  if Code <> 0 then begin
    MsgBox('Failed to download ClassicUO Launcher. Please check your internet connection and try again.' + #13#10 +
           'Error code: ' + IntToStr(Code), mbError, MB_OK);
    exit;
  end;

  if not FileExists(InstallDir + '\ClassicUOLauncher.exe') then begin
    MsgBox('ClassicUO Launcher downloaded but could not be found at the expected location.' + #13#10 +
           'Looked in: ' + InstallDir, mbError, MB_OK);
    exit;
  end;

  // ── Step 2: Download Razor Enhanced ───────────────────────────────────────
  SetStatus('Downloading Razor Enhanced (this may take a minute)...');
  Code := RunPS(
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12;' +
    'try {' +
    '  New-Item -ItemType Directory -Path "' + PluginsDir + '" -Force | Out-Null;' +
    '  $r = Invoke-RestMethod -Uri "https://api.github.com/repos/RazorEnhanced/RazorEnhanced/releases/latest";' +
    '  $a = $r.assets | Where-Object { $_.name -like "RazorEnhanced-*.zip" } | Select-Object -First 1;' +
    '  $wc = New-Object System.Net.WebClient;' +
    '  $wc.DownloadFile($a.browser_download_url, "$env:TEMP\razor.zip");' +
    '  Expand-Archive -Path "$env:TEMP\razor.zip" -DestinationPath "' + PluginsDir + '" -Force;' +
    '  Remove-Item "$env:TEMP\razor.zip" -Force;' +
    '  $exe = (Get-ChildItem -Path "' + PluginsDir + '" -Filter "RazorEnhanced.exe" -Recurse | Select-Object -First 1).FullName;' +
    '  if ($exe) { $exe | Set-Content -Path "' + TmpPath + '\razorpath.txt" -Encoding UTF8 };' +
    '  exit 0' +
    '} catch { exit 1 }'
  );

  if Code <> 0 then begin
    MsgBox('Failed to download Razor Enhanced. The launcher will still work but Razor Enhanced will not be installed.' + #13#10 +
           'You can install it manually later from razorenhanced.net', mbInformation, MB_OK);
  end;

  // Read actual plugin path
  PluginPath := PluginsDir + '\RazorEnhanced.exe';
  if LoadStringsFromFile(TmpPath + '\razorpath.txt', Lines) then
    if GetArrayLength(Lines) > 0 then
      PluginPath := Trim(Lines[0]);

  // ── Step 3: Configure launcher database ───────────────────────────────────
  SetStatus('Configuring AIther profile...');

  ForceDirectories(ExpandConstant('{userappdata}') + '\ClassicUOLauncher');

  // Create schema
  RunSQLite(LauncherDB, 'CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);');
  RunSQLite(LauncherDB, 'CREATE TABLE IF NOT EXISTS profiles (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, sort_order INTEGER NOT NULL, cuo_path TEXT, username TEXT, password TEXT, server TEXT, port TEXT, charname TEXT, client_version TEXT, uo_path TEXT, server_type INTEGER DEFAULT 0, last_server_index INTEGER DEFAULT 0, last_server_name TEXT, debug INTEGER DEFAULT 0, profiler INTEGER DEFAULT 0, save_account INTEGER DEFAULT 0, skip_login_screen INTEGER DEFAULT 0, autologin INTEGER DEFAULT 0, reconnect INTEGER DEFAULT 0, reconnect_time INTEGER DEFAULT 1000, has_music INTEGER DEFAULT 1, high_dpi INTEGER DEFAULT 0, use_verdata INTEGER DEFAULT 0, uo_protocol INTEGER DEFAULT 0, music_volume INTEGER DEFAULT 50, encryption_type INTEGER DEFAULT 0, force_driver INTEGER DEFAULT 0, packet_log INTEGER DEFAULT 0, args TEXT DEFAULT '''');');
  RunSQLite(LauncherDB, 'CREATE TABLE IF NOT EXISTS profile_plugins (id INTEGER PRIMARY KEY AUTOINCREMENT, profile_id INTEGER NOT NULL, path TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1, FOREIGN KEY(profile_id) REFERENCES profiles(id) ON DELETE CASCADE);');
  RunSQLite(LauncherDB, 'CREATE TABLE IF NOT EXISTS web_server_history (server_id INTEGER PRIMARY KEY, last_played_at INTEGER NOT NULL);');

  // Remove old profiles (AIther or any Fun Stuff UO leftovers)
  RunSQLite(LauncherDB, 'DELETE FROM profile_plugins WHERE profile_id IN (SELECT id FROM profiles WHERE name IN (''AIther'',''Fun Stuff UO'',''FunStuffUO''));');
  RunSQLite(LauncherDB, 'DELETE FROM profiles WHERE name IN (''AIther'',''Fun Stuff UO'',''FunStuffUO'');');

  // Insert fresh AIther profile — cuo_path = InstallDir (where launcher exe is)
  RunSQLite(LauncherDB,
    'INSERT INTO profiles (name, sort_order, cuo_path, server, port, client_version, uo_path, encryption_type, last_server_name) ' +
    'VALUES (''AIther'', 0, ''' + CUOPath + ''', ''aither-uo.com'', ''2593'', ''7.0.115.0'', ''' + UOPath + ''', 0, ''AIther'');'
  );

  // Wire up Razor Enhanced and select profile
  RunPS(
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12;' +
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

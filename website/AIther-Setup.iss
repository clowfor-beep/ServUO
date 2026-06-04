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

  // Write PS1 script to temp file to avoid inline escaping issues
  SaveStringToFile(TmpPath + '\dl_cuo.ps1',
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12' + #13#10 +
    '$url  = "https://github.com/ClassicUO/deploy/releases/download/launcher-release/ClassicUOLauncher-win-x64-release.zip"' + #13#10 +
    '$dest = "' + InstallDir + '"' + #13#10 +
    '$zip  = "$env:TEMP\cuo.zip"' + #13#10 +
    '$wc   = New-Object System.Net.WebClient' + #13#10 +
    '$wc.Headers.Add("User-Agent", "Mozilla/5.0")' + #13#10 +
    '$wc.DownloadFile($url, $zip)' + #13#10 +
    'Expand-Archive -Path $zip -DestinationPath $dest -Force' + #13#10 +
    'Remove-Item $zip -Force' + #13#10 +
    '$items = Get-ChildItem -Path $dest' + #13#10 +
    'if ($items.Count -eq 1 -and $items[0].PSIsContainer) {' + #13#10 +
    '  $sub = $items[0].FullName' + #13#10 +
    '  Get-ChildItem -Path $sub | Move-Item -Destination $dest -Force' + #13#10 +
    '  Remove-Item $sub -Force -Recurse' + #13#10 +
    '}',
    False);

  Exec('powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + TmpPath + '\dl_cuo.ps1"',
    '', SW_HIDE, ewWaitUntilTerminated, Code);

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

  SaveStringToFile(TmpPath + '\dl_razor.ps1',
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12' + #13#10 +
    'New-Item -ItemType Directory -Path "' + PluginsDir + '" -Force | Out-Null' + #13#10 +
    '$wc = New-Object System.Net.WebClient' + #13#10 +
    '$wc.Headers.Add("User-Agent", "Mozilla/5.0")' + #13#10 +
    '$r = Invoke-RestMethod -Uri "https://api.github.com/repos/RazorEnhanced/RazorEnhanced/releases/latest"' + #13#10 +
    '$a = $r.assets | Where-Object { $_.name -like "RazorEnhanced-*.zip" } | Select-Object -First 1' + #13#10 +
    '$wc.DownloadFile($a.browser_download_url, "$env:TEMP\razor.zip")' + #13#10 +
    'Expand-Archive -Path "$env:TEMP\razor.zip" -DestinationPath "' + PluginsDir + '" -Force' + #13#10 +
    'Remove-Item "$env:TEMP\razor.zip" -Force' + #13#10 +
    '$exe = (Get-ChildItem -Path "' + PluginsDir + '" -Filter "RazorEnhanced.exe" -Recurse | Select-Object -First 1).FullName' + #13#10 +
    'if ($exe) { $exe | Set-Content -Path "' + TmpPath + '\razorpath.txt" -Encoding UTF8 }',
    False);

  Exec('powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + TmpPath + '\dl_razor.ps1"',
    '', SW_HIDE, ewWaitUntilTerminated, Code);

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

  // Write a PS1 that pipes SQL directly to sqlite3 stdin — avoids all path/quoting issues
  SaveStringToFile(TmpPath + '\setup_db.ps1',
    '$sq  = "' + SqlitePath + '"' + #13#10 +
    '$db  = "' + LauncherDB + '"' + #13#10 +
    '$cuo = "' + CUOPath + '"' + #13#10 +
    '$uo  = "' + UOPath + '"' + #13#10 +
    '$plg = "' + PluginPath + '"' + #13#10 +
    '' + #13#10 +
    '# Delete old DB so no stale profiles remain' + #13#10 +
    'if (Test-Path $db) { Remove-Item $db -Force }' + #13#10 +
    '' + #13#10 +
    '# Build SQL as a string and pipe to sqlite3 stdin' + #13#10 +
    '$sql = @"' + #13#10 +
    'CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);' + #13#10 +
    'CREATE TABLE IF NOT EXISTS profiles (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, sort_order INTEGER NOT NULL, cuo_path TEXT, server TEXT, port TEXT, client_version TEXT, uo_path TEXT, encryption_type INTEGER DEFAULT 0, last_server_name TEXT);' + #13#10 +
    'CREATE TABLE IF NOT EXISTS profile_plugins (id INTEGER PRIMARY KEY AUTOINCREMENT, profile_id INTEGER NOT NULL, path TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1);' + #13#10 +
    'INSERT INTO profiles (name, sort_order, cuo_path, server, port, client_version, uo_path, encryption_type, last_server_name) VALUES ("AIther", 0, "$cuo", "aither-uo.com", "2593", "7.0.115.0", "$uo", 0, "AIther");' + #13#10 +
    'INSERT OR REPLACE INTO settings (key, value) VALUES ("selected_profile_index", "0");' + #13#10 +
    'INSERT OR REPLACE INTO settings (key, value) VALUES ("last_profile_name", "AIther");' + #13#10 +
    '"@' + #13#10 +
    '' + #13#10 +
    '$sql | & $sq $db' + #13#10 +
    '' + #13#10 +
    '# Add Razor plugin' + #13#10 +
    '$id = (& $sq $db "SELECT id FROM profiles ORDER BY id DESC LIMIT 1;").Trim()' + #13#10 +
    'if ($id) { "INSERT OR REPLACE INTO profile_plugins (profile_id, path, enabled) VALUES ($id, ''$plg'', 1);" | & $sq $db }',
    False);

  Exec('powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + TmpPath + '\setup_db.ps1"',
    '', SW_HIDE, ewWaitUntilTerminated, Code);

  SetStatus('Done!');
end;

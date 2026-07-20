[Setup]
AppName=STORM SWITCH BOX
AppVersion=3.8.8
AppPublisher=ReiKatari
AppPublisherURL=https://github.com/ReiKatari/STORM_SWITCH_BOX
DefaultDirName={localappdata}\Programs\STORM_SWITCH_BOX
DefaultGroupName=STORM_SWITCH_BOX
OutputBaseFilename=STORM_SWITCH_BOX_3.8.8_Setup
SetupIconFile=..\storm_switch_box.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
UninstallDisplayIcon={app}\StormSwitchBox.exe
PrivilegesRequired=lowest

[Types]
Name: "full"; Description: "Стандартная установка"
Name: "portable"; Description: "Портативная распаковка"

[Components]
Name: "full"; Description: "Стандартная установка"; Types: full
Name: "portable"; Description: "Портативная распаковка"; Types: portable

[Files]
; Source files from publish output
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\StormSwitchBox.pri"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\STORM_SWITCH_BOX"; Filename: "{app}\StormSwitchBox.exe"; WorkingDir: "{app}"; Components: full
Name: "{autodesktop}\STORM_SWITCH_BOX"; Filename: "{app}\StormSwitchBox.exe"; WorkingDir: "{app}"; Components: full; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Components: full
Name: "contextmenu"; Description: "Добавить пункты в контекстное меню"; Components: full

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Code]
var
  BackupSettingsExist: Boolean;
  BackupHistoryExist: Boolean;

function InitializeSetup(): Boolean;
var
  UninstallKey: string;
  UninstallString: string;
  InstallLocation: string;
  ResultCode: Integer;
  BackupDir: string;
  Found: Boolean;
begin
  Result := True;
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\STORM SWITCH BOX_is1';
  BackupSettingsExist := False;
  BackupHistoryExist := False;
  UninstallString := '';
  InstallLocation := '';
  Found := False;

  Log('SSB_Update: InitializeSetup started.');

  // 1. Проверяем HKCU
  if RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', UninstallString) then
  begin
    RegQueryStringValue(HKCU, UninstallKey, 'InstallLocation', InstallLocation);
    Found := True;
    Log('SSB_Update: Found old installation in HKCU.');
  end
  // 2. Проверяем HKLM64
  else if RegQueryStringValue(HKLM64, UninstallKey, 'UninstallString', UninstallString) then
  begin
    RegQueryStringValue(HKLM64, UninstallKey, 'InstallLocation', InstallLocation);
    Found := True;
    Log('SSB_Update: Found old installation in HKLM64.');
  end
  // 3. Проверяем HKLM32
  else if RegQueryStringValue(HKLM32, UninstallKey, 'UninstallString', UninstallString) then
  begin
    RegQueryStringValue(HKLM32, UninstallKey, 'InstallLocation', InstallLocation);
    Found := True;
    Log('SSB_Update: Found old installation in HKLM32.');
  end
  // 4. Проверяем HKLM (на всякий случай)
  else if RegQueryStringValue(HKLM, UninstallKey, 'UninstallString', UninstallString) then
  begin
    RegQueryStringValue(HKLM, UninstallKey, 'InstallLocation', InstallLocation);
    Found := True;
    Log('SSB_Update: Found old installation in HKLM.');
  end;

  if Found then
  begin
    Log('SSB_Update: Old InstallLocation = ' + InstallLocation);
    Log('SSB_Update: Old UninstallString = ' + UninstallString);

    if (UninstallString <> '') and (InstallLocation <> '') then
    begin
      BackupDir := ExpandConstant('{tmp}\SSB_Backup');
      CreateDir(BackupDir);
      Log('SSB_Update: Created backup directory ' + BackupDir);
      
      // Резервное копирование настроек
      if FileExists(InstallLocation + '\ssb_native.settings.json') then
      begin
        if CopyFile(InstallLocation + '\ssb_native.settings.json', BackupDir + '\ssb_native.settings.json', False) then
        begin
          BackupSettingsExist := True;
          Log('SSB_Update: Settings backup created successfully.');
        end
        else
          Log('SSB_Update: Failed to backup settings.');
      end
      else
        Log('SSB_Update: Settings file ssb_native.settings.json not found in old installation.');
      
      // Резервное копирование истории
      if FileExists(InstallLocation + '\history.json') then
      begin
        if CopyFile(InstallLocation + '\history.json', BackupDir + '\history.json', False) then
        begin
          BackupHistoryExist := True;
          Log('SSB_Update: History backup created successfully.');
        end
        else
          Log('SSB_Update: Failed to backup history.');
      end
      else
        Log('SSB_Update: History file history.json not found in old installation.');

      // Очищаем путь от кавычек
      UninstallString := RemoveQuotes(UninstallString);
      Log('SSB_Update: Executing quiet uninstall via ShellExec: ' + UninstallString);
      
      // Запускаем тихий деинсталлятор через ShellExec для поддержки UAC-подъема
      if ShellExec('open', UninstallString, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      begin
        Log('SSB_Update: Uninstall completed with ResultCode = ' + IntToStr(ResultCode));
      end
      else
      begin
        Log('SSB_Update: Failed to execute uninstall. Error code = ' + SysErrorMessage(DllGetLastError()));
      end;
    end;
  end
  else
  begin
    Log('SSB_Update: No previous installation found in registry.');
  end;
end;

procedure CreateDirectCommand(Association: string; Verb: string; LabelName: string; Action: string);
var
  Path: string;
  Cmd: string;
begin
  Path := 'Software\Classes\' + Association + '\shell\StormSwitchBox\shell\' + Verb;
  RegWriteStringValue(HKCU, Path, 'MUIVerb', LabelName);
  Cmd := '"' + ExpandConstant('{app}') + '\StormSwitchBox.exe" --action ' + Action + ' "%1"';
  RegWriteStringValue(HKCU, Path + '\command', '', Cmd);
end;

procedure CreateFormatCommands(Association: string; Verb: string; LabelName: string; Action: string);
var
  Formats: array[0..3] of string;
  I: Integer;
  Path: string;
  SubPath: string;
  Cmd: string;
  Fmt: string;
begin
  Formats[0] := 'NSP';
  Formats[1] := 'NSZ';
  Formats[2] := 'XCI';
  Formats[3] := 'XCZ';
  
  Path := 'Software\Classes\' + Association + '\shell\StormSwitchBox\shell\' + Verb;
  RegWriteStringValue(HKCU, Path, 'MUIVerb', LabelName);
  
  for I := 0 to 3 do
  begin
    Fmt := Formats[I];
    SubPath := Path + '\shell\' + Fmt;
    RegWriteStringValue(HKCU, SubPath, 'MUIVerb', 'в формат ' + Fmt);
    Cmd := '"' + ExpandConstant('{app}') + '\StormSwitchBox.exe" --action ' + Action + ' --format ' + Fmt + ' "%1"';
    RegWriteStringValue(HKCU, SubPath + '\command', '', Cmd);
  end;
end;

procedure RegisterForAssociation(Association: string);
begin
  RegWriteStringValue(HKCU, 'Software\Classes\' + Association + '\shell\StormSwitchBox', 'MUIVerb', 'STORM SWITCH BOX');
  RegWriteStringValue(HKCU, 'Software\Classes\' + Association + '\shell\StormSwitchBox', 'Icon', ExpandConstant('{app}') + '\StormSwitchBox.exe');
  
  CreateFormatCommands(Association, '01update', 'Обновление', 'update');
  CreateDirectCommand(Association, '02unpack', 'Распаковка', 'unpack');
  CreateFormatCommands(Association, '03pack', 'Упаковка', 'pack');
  CreateFormatCommands(Association, '04convert', 'Конвертация', 'convert');
  CreateFormatCommands(Association, '05multi', 'Мульти-контент', 'multi');
end;

procedure RegisterAllContextMenus();
begin
  RegisterForAssociation('SystemFileAssociations\.nsp');
  RegisterForAssociation('SystemFileAssociations\.nsz');
  RegisterForAssociation('SystemFileAssociations\.xci');
  RegisterForAssociation('SystemFileAssociations\.xcz');
  RegisterForAssociation('Directory');
end;

procedure UnregisterAllContextMenus();
begin
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\StormSwitchBox');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  BackupDir: string;
  AppDir: string;
begin
  if CurStep = ssPostInstall then
  begin
    AppDir := ExpandConstant('{app}');
    BackupDir := ExpandConstant('{tmp}\SSB_Backup');

    // Восстановление настроек
    if BackupSettingsExist and FileExists(BackupDir + '\ssb_native.settings.json') then
    begin
      CopyFile(BackupDir + '\ssb_native.settings.json', AppDir + '\ssb_native.settings.json', False);
      Log('SSB_Update: Restored settings successfully.');
    end;

    // Восстановление истории
    if BackupHistoryExist and FileExists(BackupDir + '\history.json') then
    begin
      CopyFile(BackupDir + '\history.json', AppDir + '\history.json', False);
      Log('SSB_Update: Restored history successfully.');
    end;

    if WizardIsComponentSelected('portable') then
      SaveStringToFile(AppDir + '\portable.marker', '', False);

    // Регистрация контекстного меню
    if WizardIsComponentSelected('full') and WizardIsTaskSelected('contextmenu') then
    begin
      Log('SSB_Setup: Registering context menus...');
      RegisterAllContextMenus();
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Удаляем ветки реестра контекстного меню
    UnregisterAllContextMenus();
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    DeleteFile(ExpandConstant('{app}\portable.marker'));
  end;
end;


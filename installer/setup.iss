[Setup]
AppName=STORM SWITCH BOX
AppVersion=3.9.33
AppPublisher=ReiKatari
AppPublisherURL=https://github.com/ReiKatari/STORM_SWITCH_BOX
DefaultDirName={localappdata}\Programs\STORM_SWITCH_BOX
DefaultGroupName=STORM_SWITCH_BOX
OutputBaseFilename=STORM_SWITCH_BOX_3.9.33_Setup
SetupIconFile=..\storm_switch_box.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
Compression=lzma2/ultra64
UninstallDisplayIcon={app}\StormSwitchBox.exe
AppMutex=StormSwitchBox_SingleInstanceMutex
CloseApplications=yes
CloseApplicationsFilter=*StormSwitchBox*
RestartApplications=no
SignTool=signtool

[Types]
Name: "full"; Description: "Стандартная установка"
Name: "portable"; Description: "Портативная распаковка"

[Components]
Name: "full"; Description: "Стандартная установка"; Types: full
Name: "portable"; Description: "Портативная распаковка"; Types: portable

[Files]
; Source files from publish output
Source: "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StormSwitchBox.pri"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

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
  // Разблокировка файла инсталлятора от Windows Smart App Control / Zone.Identifier
  Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path ''' + ExpandConstant('{srcexe}') + '''"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // Принудительное завершение работающих экземпляров приложения перед установкой/заменой файлов
  Exec('taskkill.exe', '/F /IM StormSwitchBox.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

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

procedure RegisterForAssociation(Association: string);
begin
  RegWriteStringValue(HKCU, 'Software\Classes\' + Association + '\shell\StormSwitchBox', 'MUIVerb', 'STORM SWITCH BOX');
  RegWriteStringValue(HKCU, 'Software\Classes\' + Association + '\shell\StormSwitchBox', 'Icon', ExpandConstant('{app}') + '\StormSwitchBox.exe');
  RegWriteStringValue(HKCU, 'Software\Classes\' + Association + '\shell\StormSwitchBox', 'SubCommands', '');
  
  CreateDirectCommand(Association, '01update', 'Обновление', 'update');
  CreateDirectCommand(Association, '02unpack', 'Распаковка', 'unpack');
  CreateDirectCommand(Association, '03pack', 'Упаковка', 'pack');
  CreateDirectCommand(Association, '04convert', 'Конвертация', 'convert');
  CreateDirectCommand(Association, '05multi', 'Мульти-контент', 'multi');
  CreateDirectCommand(Association, '06verify', 'Проверка', 'verify');
end;

procedure RegisterAllContextMenus();
begin
  RegisterForAssociation('SystemFileAssociations\.nsp');
  RegisterForAssociation('SystemFileAssociations\.nsz');
  RegisterForAssociation('SystemFileAssociations\.xci');
  RegisterForAssociation('SystemFileAssociations\.xcz');
  RegisterForAssociation('.nsp');
  RegisterForAssociation('.nsz');
  RegisterForAssociation('.xci');
  RegisterForAssociation('.xcz');
  RegisterForAssociation('Directory');
end;

procedure UnregisterAllContextMenus();
begin
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.nsp\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.nsz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.xci\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.xcz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\*\shell\StormSwitchBox');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResCode: Integer;
  BackupDir: string;
  AppDir: string;
begin
  if CurStep = ssPostInstall then
  begin
    BackupDir := ExpandConstant('{userappdata}\StormSwitchBoxBackup');
    AppDir := ExpandConstant('{app}');
    if DirExists(BackupDir) then
    begin
      if FileExists(BackupDir + '\ssb_native.settings.json') then
        FileCopy(BackupDir + '\ssb_native.settings.json', AppDir + '\ssb_native.settings.json', True);
      if FileExists(BackupDir + '\history.json') then
        FileCopy(BackupDir + '\history.json', AppDir + '\history.json', True);
      DelTree(BackupDir, True, True, True);
      Log('SSB_Update: Restored history successfully.');
    end;

    if WizardIsComponentSelected('portable') then
      SaveStringToFile(AppDir + '\portable.marker', '', False);

    // Регистрация контекстного меню
    if WizardIsComponentSelected('full') and WizardIsTaskSelected('contextmenu') then
    begin
      Log('SSB_Setup: Cleaning legacy context menus...');
      UnregisterAllContextMenus();
      Log('SSB_Setup: Registering context menus...');
      RegisterAllContextMenus();
    end;

    // Снятие интернет-блокировки Zone.Identifier со всех установленных файлов
    Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path ''' + ExpandConstant('{app}') + ''' -Recurse | Unblock-File"', '', SW_HIDE, ewWaitUntilTerminated, ResCode);
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


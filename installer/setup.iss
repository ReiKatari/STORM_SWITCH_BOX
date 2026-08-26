[Setup]
AppName=STORM SWITCH BOX
AppVersion=4.7.8
AppPublisher=STORM CHANNEL & ReiKatari
AppPublisherURL=https://rutube.ru/channel/42609927/
DefaultDirName={autopf}\STORM SWITCH BOX
DefaultGroupName=STORM SWITCH BOX
OutputBaseFilename=STORM_SWITCH_BOX_4.7.8_Setup
OutputDir=Output
SetupIconFile=..\storm_switch_box.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
Compression=lzma2/ultra64
UninstallDisplayIcon={app}\StormSwitchBox.exe
AppMutex=StormSwitchBox_SingleInstanceMutex
CloseApplications=no
RestartApplications=no
WizardStyle=modern
ShowLanguageDialog=no

[Types]
Name: "full"; Description: "Стандартная установка"
Name: "portable"; Description: "Портативная распаковка"

[Components]
Name: "full"; Description: "Стандартная установка"; Types: full
Name: "portable"; Description: "Портативная распаковка"; Types: portable

[Files]
Source: "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StormSwitchBox.pri"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\STORM_SWITCH_BOX"; Filename: "{app}\StormSwitchBox.exe"; WorkingDir: "{app}"; Components: full; Tasks: startmenu
Name: "{autodesktop}\STORM_SWITCH_BOX"; Filename: "{app}\StormSwitchBox.exe"; WorkingDir: "{app}"; Components: full; Tasks: desktopicon

[Tasks]
Name: "startmenu"; Description: "Добавить в меню «Пуск»"; Components: full
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Components: full
Name: "contextmenu"; Description: "Добавить пункты в контекстное меню Проводника"; Components: full

[Run]
Filename: "{app}\StormSwitchBox.exe"; Description: "Запустить STORM SWITCH BOX"; Flags: postinstall nowait

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Code]

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM StormSwitchBox.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM STORM_SWITCH_BOX.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
  Result := True;
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
  RegisterForAssociation('SystemFileAssociations\.3ds');
  RegisterForAssociation('SystemFileAssociations\.cci');
  RegisterForAssociation('SystemFileAssociations\.cia');
  RegisterForAssociation('SystemFileAssociations\.cxi');
  RegisterForAssociation('.nsp');
  RegisterForAssociation('.nsz');
  RegisterForAssociation('.xci');
  RegisterForAssociation('.xcz');
  RegisterForAssociation('.3ds');
  RegisterForAssociation('.cci');
  RegisterForAssociation('.cia');
  RegisterForAssociation('.cxi');
  RegisterForAssociation('Directory');
end;

procedure UnregisterAllContextMenus();
begin
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.3ds\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.cci\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.cia\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\SystemFileAssociations\.cxi\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.nsp\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.nsz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.xci\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.xcz\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.3ds\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.cci\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.cia\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.cxi\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\StormSwitchBox');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\*\shell\StormSwitchBox');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
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
        FileCopy(BackupDir + '\ssb_native.settings.json', AppDir + '\ssb_native.settings.json', False);
      if FileExists(BackupDir + '\history.json') then
        FileCopy(BackupDir + '\history.json', AppDir + '\history.json', False);
      DelTree(BackupDir, True, True, True);
    end;

    if WizardIsComponentSelected('portable') then
      SaveStringToFile(AppDir + '\portable.marker', '', False);

    if WizardIsComponentSelected('full') and WizardIsTaskSelected('contextmenu') then
    begin
      UnregisterAllContextMenus();
      RegisterAllContextMenus();
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    UnregisterAllContextMenus();
  if CurUninstallStep = usPostUninstall then
    DeleteFile(ExpandConstant('{app}\portable.marker'));
end;

[Setup]
AppName=STORM SWITCH BOX
AppVersion=4.1.1
AppPublisher=STORM CHANNEL & ReiKatari
AppPublisherURL=https://rutube.ru/channel/42609927/
DefaultDirName={localappdata}\Programs\STORM_SWITCH_BOX
DefaultGroupName=STORM_SWITCH_BOX
OutputBaseFilename=STORM_SWITCH_BOX_4.1.1_Setup
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
Source: "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StormSwitchBox.pri"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\STORM_SWITCH_BOX"; Filename: "{app}\StormSwitchBox.exe"; WorkingDir: "{app}"; Components: full; Tasks: startmenu
Name: "{autodesktop}\STORM_SWITCH_BOX"; Filename: "{app}\StormSwitchBox.exe"; WorkingDir: "{app}"; Components: full; Tasks: desktopicon

[Tasks]
Name: "startmenu"; Description: "Добавить в меню «Пуск»"; Components: full
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Components: full
Name: "contextmenu"; Description: "Добавить пункты в контекстное меню Explorer"; Components: full

[Run]
Filename: "{app}\StormSwitchBox.exe"; Description: "🚀 Запустить STORM SWITCH BOX v4.1.1"; Flags: postinstall nowait
Filename: "https://rutube.ru/channel/42609927/"; Description: "📺 Открыть официальный канал STORM CHANNEL на RuTube"; Flags: postinstall shellexec unchecked

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Code]
var
  ChannelLinkLabel: TLabel;

procedure ChannelLinkClick(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', 'https://rutube.ru/channel/42609927/', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure ApplyCustomStylesToWizard();
begin
  // Синевато-циановые стили заголовков шапки
  WizardForm.PageNameLabel.Font.Color := $C86400;
  WizardForm.PageNameLabel.Font.Style := [fsBold];

  WizardForm.WelcomeLabel1.Font.Color := $C86400;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];

  WizardForm.FinishedHeadingLabel.Font.Color := $C86400;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
end;

procedure InitializeWizard();
begin
  ApplyCustomStylesToWizard();

  // Ссылка на STORM CHANNEL внизу слева окна инсталлятора
  ChannelLinkLabel := TLabel.Create(WizardForm);
  ChannelLinkLabel.Parent := WizardForm;
  ChannelLinkLabel.Left := 16;
  ChannelLinkLabel.Top := WizardForm.CancelButton.Top + 4;
  ChannelLinkLabel.Caption := '📺 STORM CHANNEL (RuTube)';
  ChannelLinkLabel.Font.Color := $C86400;
  ChannelLinkLabel.Font.Style := [fsBold, fsUnderline];
  ChannelLinkLabel.Cursor := crHand;
  ChannelLinkLabel.OnClick := @ChannelLinkClick;
end;

function InitializeSetup(): Boolean;
begin
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
        CopyFile(BackupDir + '\ssb_native.settings.json', AppDir + '\ssb_native.settings.json', True);
      if FileExists(BackupDir + '\history.json') then
        CopyFile(BackupDir + '\history.json', AppDir + '\history.json', True);
      DelTree(BackupDir, True, True, True);
      Log('SSB_Update: Restored history successfully.');
    end;

    if WizardIsComponentSelected('portable') then
      SaveStringToFile(AppDir + '\portable.marker', '', False);

    if WizardIsComponentSelected('full') and WizardIsTaskSelected('contextmenu') then
    begin
      Log('SSB_Setup: Cleaning legacy context menus...');
      UnregisterAllContextMenus();
      Log('SSB_Setup: Registering context menus...');
      RegisterAllContextMenus();
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    UnregisterAllContextMenus();
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    DeleteFile(ExpandConstant('{app}\portable.marker'));
  end;
end;

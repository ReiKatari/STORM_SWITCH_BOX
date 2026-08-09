[Setup]
AppName=STORM SWITCH BOX
AppVersion=4.0.9
AppPublisher=STORM CHANNEL & ReiKatari
AppPublisherURL=https://rutube.ru/channel/42609927/
DefaultDirName={localappdata}\Programs\STORM_SWITCH_BOX
DefaultGroupName=STORM_SWITCH_BOX
OutputBaseFilename=STORM_SWITCH_BOX_4.0.9_Setup
SetupIconFile=..\storm_switch_box.ico
WizardSmallImageFile=..\storm_switch_box.ico
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
Filename: "{app}\StormSwitchBox.exe"; Description: "🚀 Запустить STORM SWITCH BOX v4.0.9"; Flags: postinstall nowait
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

procedure ApplyDarkThemeToWizard();
var
  BgColor: TColor;
begin
  // Тёмная тема инсталлятора STORM ($BBGGRR формат в Pascal: R=$12, G=$0F, B=$1A)
  BgColor := $1A0F12;
  
  WizardForm.WelcomePage.Color := BgColor;
  WizardForm.FinishedPage.Color := BgColor;
  WizardForm.InnerPage.Color := BgColor;

  WizardForm.PageNameLabel.Font.Color := $00F0FF;
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageDescriptionLabel.Font.Color := $E0E0E0;

  WizardForm.WelcomeLabel1.Font.Color := $09EEFC;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.WelcomeLabel2.Font.Color := $E0E0E0;

  WizardForm.FinishedHeadingLabel.Font.Color := $09EEFC;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
  WizardForm.FinishedLabel.Font.Color := $E0E0E0;
end;

procedure InitializeWizard();
begin
  ApplyDarkThemeToWizard();

  // Создаем ссылку на STORM CHANNEL внизу слева окна инсталлятора
  ChannelLinkLabel := TLabel.Create(WizardForm);
  ChannelLinkLabel.Parent := WizardForm;
  ChannelLinkLabel.Left := 16;
  ChannelLinkLabel.Top := WizardForm.CancelButton.Top + 4;
  ChannelLinkLabel.Caption := '📺 STORM CHANNEL (RuTube)';
  ChannelLinkLabel.Font.Color := $00F0FF;
  ChannelLinkLabel.Font.Style := [fsBold, fsUnderline];
  ChannelLinkLabel.Cursor := crHand;
  ChannelLinkLabel.OnClick := @ChannelLinkClick;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path ''' + ExpandConstant('{srcexe}') + '''"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM StormSwitchBox.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
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

    Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path ''' + ExpandConstant('{app}') + ''' -Recurse | Unblock-File"', '', SW_HIDE, ewWaitUntilTerminated, ResCode);
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

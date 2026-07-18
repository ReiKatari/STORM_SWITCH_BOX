[Setup]
AppName=STORM SWITCH BOX
AppVersion=3.8.2
AppPublisher=ReiKatari
AppPublisherURL=https://github.com/ReiKatari/STORM_SWITCH_BOX
DefaultDirName={autopf}\STORM SWITCH BOX
DefaultGroupName=STORM SWITCH BOX
OutputBaseFilename=STORM_SWITCH_BOX_3.8.2_Setup
SetupIconFile=..\storm_switch_box.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
UninstallDisplayIcon={app}\StormSwitchBox.exe

[Types]
Name: "full"; Description: "Стандартная установка"
Name: "portable"; Description: "Портативная распаковка"

[Components]
Name: "full"; Description: "Стандартная установка"; Types: full
Name: "portable"; Description: "Портативная распаковка"; Types: portable

[Files]
; Source files from publish output
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\storm_switch_box.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\STORM SWITCH BOX"; Filename: "{app}\StormSwitchBox.exe"; Components: full
Name: "{autodesktop}\STORM SWITCH BOX"; Filename: "{app}\StormSwitchBox.exe"; Components: full; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Components: full
Name: "contextmenu"; Description: "Добавить пункты в контекстное меню"; Components: full

[Registry]
; Context Menu Extensions - only for full install

; .nsp
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox"; ValueType: string; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi ""%1"""; Components: full; Tasks: contextmenu

; .nsz
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox"; ValueType: string; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi ""%1"""; Components: full; Tasks: contextmenu

; .xci
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox"; ValueType: string; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi ""%1"""; Components: full; Tasks: contextmenu

; .xcz
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox"; ValueType: string; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi ""%1"""; Components: full; Tasks: contextmenu

; Directory
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox"; ValueType: string; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\03pack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\04convert\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert ""%1"""; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\StormSwitchBox\shell\05multi\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi ""%1"""; Components: full; Tasks: contextmenu

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if IsComponentSelected('portable') then
      SaveStringToFile(ExpandConstant('{app}\portable.marker'), '', False);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DeleteFile(ExpandConstant('{app}\portable.marker'));
  end;
end;

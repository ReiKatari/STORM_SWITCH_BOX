[Setup]
AppName=STORM SWITCH BOX
AppVersion=3.8.5
AppPublisher=ReiKatari
AppPublisherURL=https://github.com/ReiKatari/STORM_SWITCH_BOX
DefaultDirName={localappdata}\Programs\STORM_SWITCH_BOX
DefaultGroupName=STORM_SWITCH_BOX
OutputBaseFilename=STORM_SWITCH_BOX_3.8.5_Setup
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
Source: "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\StormSwitchBox.pri"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\STORM_SWITCH_BOX"; Filename: "{app}\StormSwitchBox.exe"; WorkingDir: "{app}"; Components: full
Name: "{autodesktop}\STORM_SWITCH_BOX"; Filename: "{app}\StormSwitchBox.exe"; WorkingDir: "{app}"; Components: full; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Components: full
Name: "contextmenu"; Description: "Добавить пункты в контекстное меню"; Components: full

[Registry]
; Context Menu Extensions - per-user (HKCU\Software\Classes = user HKCR)

; SystemFileAssociations\.nsp
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox"; ValueType: string; ValueName: "MUIVerb"; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\03pack\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\04convert\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsp\shell\StormSwitchBox\shell\05multi\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCZ ""%1"""; Components: full; Tasks: contextmenu

; SystemFileAssociations\.nsz
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox"; ValueType: string; ValueName: "MUIVerb"; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\03pack\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\04convert\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.nsz\shell\StormSwitchBox\shell\05multi\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCZ ""%1"""; Components: full; Tasks: contextmenu

; SystemFileAssociations\.xci
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox"; ValueType: string; ValueName: "MUIVerb"; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\03pack\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\04convert\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xci\shell\StormSwitchBox\shell\05multi\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCZ ""%1"""; Components: full; Tasks: contextmenu

; SystemFileAssociations\.xcz
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox"; ValueType: string; ValueName: "MUIVerb"; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\03pack\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\04convert\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.xcz\shell\StormSwitchBox\shell\05multi\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCZ ""%1"""; Components: full; Tasks: contextmenu

; Directory
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox"; ValueType: string; ValueName: "MUIVerb"; ValueData: "STORM SWITCH BOX"; Components: full; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\StormSwitchBox.exe"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\01update"; ValueType: string; ValueData: "Обновление"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\01update\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action update ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\02unpack"; ValueType: string; ValueData: "Распаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\02unpack\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action unpack ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueData: "Упаковка"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\03pack\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action pack --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueData: "Конвертация"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\04convert\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action convert --format XCZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueData: "Мульти-контент"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi\shell\01nsp"; ValueType: string; ValueData: "в формат NSP"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi\shell\01nsp\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSP ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi\shell\02nsz"; ValueType: string; ValueData: "в формат NSZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi\shell\02nsz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format NSZ ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi\shell\03xci"; ValueType: string; ValueData: "в формат XCI"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi\shell\03xci\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCI ""%1"""; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi\shell\04xcz"; ValueType: string; ValueData: "в формат XCZ"; Components: full; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\StormSwitchBox\shell\05multi\shell\04xcz\command"; ValueType: string; ValueData: """{app}\StormSwitchBox.exe"" --action multi --format XCZ ""%1"""; Components: full; Tasks: contextmenu



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
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DeleteFile(ExpandConstant('{app}\portable.marker'));
  end;
end;

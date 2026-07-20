@echo off
setlocal EnableDelayedExpansion

set "APP=C:\Users\ReiKatari\AppData\Local\Programs\STORM_SWITCH_BOX\StormSwitchBox.exe"

for %%A in (".nsp" ".nsz" ".xci" ".xcz" "Directory") do (
    if "%%~A"=="Directory" (
        set "BASE=HKCU\Software\Classes\Directory\shell\StormSwitchBox"
    ) else (
        set "BASE=HKCU\Software\Classes\SystemFileAssociations\%%~A\shell\StormSwitchBox"
    )
    
    reg delete "!BASE!" /f >nul 2>&1
    
    reg add "!BASE!" /v "MUIVerb" /t REG_SZ /d "STORM SWITCH BOX" /f
    reg add "!BASE!" /v "Icon" /t REG_SZ /d "!APP!" /f
    reg add "!BASE!" /v "SubCommands" /t REG_SZ /d "" /f
    
    reg add "!BASE!\shell\01update" /v "MUIVerb" /t REG_SZ /d "Обновление" /f
    reg add "!BASE!\shell\01update" /v "SubCommands" /t REG_SZ /d "" /f
    for %%F in (NSP NSZ XCI XCZ) do (
        reg add "!BASE!\shell\01update\shell\%%F" /v "MUIVerb" /t REG_SZ /d "в формат %%F" /f
        reg add "!BASE!\shell\01update\shell\%%F\command" /ve /t REG_SZ /d "\"!APP!\" --action update --format %%F \"%%1\"" /f
    )
    
    reg add "!BASE!\shell\02unpack" /v "MUIVerb" /t REG_SZ /d "Распаковка" /f
    reg add "!BASE!\shell\02unpack\command" /ve /t REG_SZ /d "\"!APP!\" --action unpack \"%%1\"" /f
    
    reg add "!BASE!\shell\03pack" /v "MUIVerb" /t REG_SZ /d "Упаковка" /f
    reg add "!BASE!\shell\03pack" /v "SubCommands" /t REG_SZ /d "" /f
    for %%F in (NSP NSZ XCI XCZ) do (
        reg add "!BASE!\shell\03pack\shell\%%F" /v "MUIVerb" /t REG_SZ /d "в формат %%F" /f
        reg add "!BASE!\shell\03pack\shell\%%F\command" /ve /t REG_SZ /d "\"!APP!\" --action pack --format %%F \"%%1\"" /f
    )
    
    reg add "!BASE!\shell\04convert" /v "MUIVerb" /t REG_SZ /d "Конвертация" /f
    reg add "!BASE!\shell\04convert" /v "SubCommands" /t REG_SZ /d "" /f
    for %%F in (NSP NSZ XCI XCZ) do (
        reg add "!BASE!\shell\04convert\shell\%%F" /v "MUIVerb" /t REG_SZ /d "в формат %%F" /f
        reg add "!BASE!\shell\04convert\shell\%%F\command" /ve /t REG_SZ /d "\"!APP!\" --action convert --format %%F \"%%1\"" /f
    )
    
    reg add "!BASE!\shell\05multi" /v "MUIVerb" /t REG_SZ /d "Мульти-контент" /f
    reg add "!BASE!\shell\05multi" /v "SubCommands" /t REG_SZ /d "" /f
    for %%F in (NSP NSZ XCI XCZ) do (
        reg add "!BASE!\shell\05multi\shell\%%F" /v "MUIVerb" /t REG_SZ /d "в формат %%F" /f
        reg add "!BASE!\shell\05multi\shell\%%F\command" /ve /t REG_SZ /d "\"!APP!\" --action multi --format %%F \"%%1\"" /f
    )
)
echo Done!

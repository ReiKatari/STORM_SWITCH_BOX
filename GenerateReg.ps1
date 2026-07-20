$appPath = "C:\\Users\\ReiKatari\\AppData\\Local\\Programs\\STORM_SWITCH_BOX\\StormSwitchBox.exe"
$content = "Windows Registry Editor Version 5.00`r`n`r`n"
$associations = @(".nsp", ".nsz", ".xci", ".xcz", "Directory")
$formats = @("NSP", "NSZ", "XCI", "XCZ")

foreach ($assoc in $associations) {
    if ($assoc -eq "Directory") {
        $base = "HKEY_CURRENT_USER\Software\Classes\Directory\shell\StormSwitchBox"
    } else {
        $base = "HKEY_CURRENT_USER\Software\Classes\SystemFileAssociations\$assoc\shell\StormSwitchBox"
    }
    
    # Delete old key
    $content += "[-$base]`r`n`r`n"
    
    # Create base
    $content += "[$base]`r`n"
    $content += "`"MUIVerb`"=`"STORM SWITCH BOX`"`r`n"
    $content += "`"Icon`"=`"$appPath`"`r`n"
    $content += "`"SubCommands`"=`"`"`r`n`r`n"
    
    # 01update
    $content += "[$base\shell\01update]`r`n"
    $content += "`"MUIVerb`"=`"Обновление`"`r`n"
    $content += "`"SubCommands`"=`"`"`r`n`r`n"
    foreach ($fmt in $formats) {
        $content += "[$base\shell\01update\shell\$fmt]`r`n"
        $content += "`"MUIVerb`"=`"в формат $fmt`"`r`n`r`n"
        $content += "[$base\shell\01update\shell\$fmt\command]`r`n"
        $content += "`"@`"=`"`"$appPath`" --action update --format $fmt `\`"%1`\`"`"`r`n`r`n"
    }
    
    # 02unpack
    $content += "[$base\shell\02unpack]`r`n"
    $content += "`"MUIVerb`"=`"Распаковка`"`r`n`r`n"
    $content += "[$base\shell\02unpack\command]`r`n"
    $content += "`"@`"=`"`"$appPath`" --action unpack `\`"%1`\`"`"`r`n`r`n"
    
    # 03pack
    $content += "[$base\shell\03pack]`r`n"
    $content += "`"MUIVerb`"=`"Упаковка`"`r`n"
    $content += "`"SubCommands`"=`"`"`r`n`r`n"
    foreach ($fmt in $formats) {
        $content += "[$base\shell\03pack\shell\$fmt]`r`n"
        $content += "`"MUIVerb`"=`"в формат $fmt`"`r`n`r`n"
        $content += "[$base\shell\03pack\shell\$fmt\command]`r`n"
        $content += "`"@`"=`"`"$appPath`" --action pack --format $fmt `\`"%1`\`"`"`r`n`r`n"
    }
    
    # 04convert
    $content += "[$base\shell\04convert]`r`n"
    $content += "`"MUIVerb`"=`"Конвертация`"`r`n"
    $content += "`"SubCommands`"=`"`"`r`n`r`n"
    foreach ($fmt in $formats) {
        $content += "[$base\shell\04convert\shell\$fmt]`r`n"
        $content += "`"MUIVerb`"=`"в формат $fmt`"`r`n`r`n"
        $content += "[$base\shell\04convert\shell\$fmt\command]`r`n"
        $content += "`"@`"=`"`"$appPath`" --action convert --format $fmt `\`"%1`\`"`"`r`n`r`n"
    }
    
    # 05multi
    $content += "[$base\shell\05multi]`r`n"
    $content += "`"MUIVerb`"=`"Мульти-контент`"`r`n"
    $content += "`"SubCommands`"=`"`"`r`n`r`n"
    foreach ($fmt in $formats) {
        $content += "[$base\shell\05multi\shell\$fmt]`r`n"
        $content += "`"MUIVerb`"=`"в формат $fmt`"`r`n`r`n"
        $content += "[$base\shell\05multi\shell\$fmt\command]`r`n"
        $content += "`"@`"=`"`"$appPath`" --action multi --format $fmt `\`"%1`\`"`"`r`n`r`n"
    }
}

$content | Out-File -FilePath "fix.reg" -Encoding Unicode
Write-Host "fix.reg generated"

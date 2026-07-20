$ErrorActionPreference = "Stop"

$appPath = "C:\Users\ReiKatari\AppData\Local\Programs\STORM_SWITCH_BOX\StormSwitchBox.exe"

$associations = @(".nsp", ".nsz", ".xci", ".xcz", "Directory")

foreach ($assoc in $associations) {
    $basePath = "HKCU:\Software\Classes\SystemFileAssociations\$assoc\shell\StormSwitchBox"
    if ($assoc -eq "Directory") {
        $basePath = "HKCU:\Software\Classes\Directory\shell\StormSwitchBox"
    }

    if (Test-Path $basePath) {
        Remove-Item -Path $basePath -Recurse -Force
    }

    New-Item -Path $basePath -Force | Out-Null
    Set-ItemProperty -Path $basePath -Name "MUIVerb" -Value "STORM SWITCH BOX"
    Set-ItemProperty -Path $basePath -Name "Icon" -Value $appPath
    
    $formats = @("NSP", "NSZ", "XCI", "XCZ")

    # 01update
    $verbPath = "$basePath\shell\01update"
    New-Item -Path $verbPath -Force | Out-Null
    Set-ItemProperty -Path $verbPath -Name "MUIVerb" -Value "Обновление"
    foreach ($fmt in $formats) {
        $sub = "$verbPath\shell\$fmt"
        New-Item -Path "$sub\command" -Force | Out-Null
        Set-ItemProperty -Path $sub -Name "MUIVerb" -Value "в формат $fmt"
        Set-ItemProperty -Path "$sub\command" -Name "(default)" -Value ('"' + $appPath + '" --action update --format ' + $fmt + ' "%1"')
    }

    # 02unpack
    $verbPath = "$basePath\shell\02unpack"
    New-Item -Path "$verbPath\command" -Force | Out-Null
    Set-ItemProperty -Path $verbPath -Name "MUIVerb" -Value "Распаковка"
    Set-ItemProperty -Path "$verbPath\command" -Name "(default)" -Value ('"' + $appPath + '" --action unpack "%1"')

    # 03pack
    $verbPath = "$basePath\shell\03pack"
    New-Item -Path $verbPath -Force | Out-Null
    Set-ItemProperty -Path $verbPath -Name "MUIVerb" -Value "Упаковка"
    foreach ($fmt in $formats) {
        $sub = "$verbPath\shell\$fmt"
        New-Item -Path "$sub\command" -Force | Out-Null
        Set-ItemProperty -Path $sub -Name "MUIVerb" -Value "в формат $fmt"
        Set-ItemProperty -Path "$sub\command" -Name "(default)" -Value ('"' + $appPath + '" --action pack --format ' + $fmt + ' "%1"')
    }

    # 04convert
    $verbPath = "$basePath\shell\04convert"
    New-Item -Path $verbPath -Force | Out-Null
    Set-ItemProperty -Path $verbPath -Name "MUIVerb" -Value "Конвертация"
    foreach ($fmt in $formats) {
        $sub = "$verbPath\shell\$fmt"
        New-Item -Path "$sub\command" -Force | Out-Null
        Set-ItemProperty -Path $sub -Name "MUIVerb" -Value "в формат $fmt"
        Set-ItemProperty -Path "$sub\command" -Name "(default)" -Value ('"' + $appPath + '" --action convert --format ' + $fmt + ' "%1"')
    }

    # 05multi
    $verbPath = "$basePath\shell\05multi"
    New-Item -Path $verbPath -Force | Out-Null
    Set-ItemProperty -Path $verbPath -Name "MUIVerb" -Value "Мульти-контент"
    foreach ($fmt in $formats) {
        $sub = "$verbPath\shell\$fmt"
        New-Item -Path "$sub\command" -Force | Out-Null
        Set-ItemProperty -Path $sub -Name "MUIVerb" -Value "в формат $fmt"
        Set-ItemProperty -Path "$sub\command" -Name "(default)" -Value ('"' + $appPath + '" --action multi --format ' + $fmt + ' "%1"')
    }
}
Write-Host "Context Menus fixed!"

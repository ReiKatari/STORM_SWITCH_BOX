$files = @(
    'e:\STORM SWITCH BOX\installer\setup.iss',
    'e:\STORM SWITCH BOX\MainWindow.xaml.cs',
    'e:\STORM SWITCH BOX\Views\InstructionPage.xaml.cs',
    'e:\STORM SWITCH BOX\Views\SettingsPage.xaml',
    'e:\STORM SWITCH BOX\Views\SettingsPage.xaml.cs'
)
foreach ($file in $files) {
    $content = Get-Content $file -Encoding UTF8
    $content = $content -replace '3\.8\.7', '3.8.9'
    Set-Content $file -Value $content -Encoding UTF8
}

$t = [System.IO.File]::ReadAllBytes('E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\TestApp\bin\x64\Debug\net8.0-windows10.0.19041.0\verify_temp\original_extracted\010057901e9e60000000000000000015.tik')
$tk = $t[0x180..0x18F]
$rid = $t[0x2A0..0x2AF]
Write-Output ("TitleKey: " + [BitConverter]::ToString($tk).Replace('-', '').ToLower())
Write-Output ("RightsId: " + [BitConverter]::ToString($rid).Replace('-', '').ToLower())


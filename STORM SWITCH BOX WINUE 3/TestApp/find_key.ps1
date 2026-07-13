$encData = [byte[]](0xD7, 0x84, 0x3D, 0x66, 0x65, 0x28, 0xFF, 0x62, 0x26, 0x90, 0x43, 0x9E, 0x1B, 0x79, 0xCB, 0x46)
$secOffset = 0xC00
$pfsOffset = 0x68000
$globalBlockIndex = ($secOffset + $pfsOffset) / 16

# Upper IV: 0000000001000000 in hex read from header, reversed is 0000000100000000
$gen = [byte[]](0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00)

$iv = New-Object byte[] 16
[Array]::Copy($gen, 0, $iv, 0, 8)
$blockBytes = [BitConverter]::GetBytes([long]$globalBlockIndex)
if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($blockBytes) }
[Array]::Copy($blockBytes, 0, $iv, 8, 8)

# Load all keys
$keys = @{}
$prodKeysPath = "E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\tools\prod.keys"
$titleKeysPath = [System.IO.Path]::Combine($env:USERPROFILE, ".switch", "title.keys")

if (Test-Path $prodKeysPath) {
    foreach ($line in Get-Content $prodKeysPath) {
        $line = $line.Trim()
        if ($line.StartsWith("#") -or $line.StartsWith(";") -or -not $line.Contains("=")) { continue }
        $parts = $line.Split("=", 2)
        $name = $parts[0].Trim()
        $val = $parts[1].Trim().Split(" ")[0]
        if ($val.Length -eq 32) {
            $keys[$name] = $val
        }
    }
}

if (Test-Path $titleKeysPath) {
    foreach ($line in Get-Content $titleKeysPath) {
        $line = $line.Trim()
        if ($line.StartsWith("#") -or $line.StartsWith(";") -or -not $line.Contains("=")) { continue }
        $parts = $line.Split("=", 2)
        $name = $parts[0].Trim()
        $val = $parts[1].Trim().Split(" ")[0]
        if ($val.Length -eq 32) {
            $keys["title_" + $name] = $val
        }
    }
}

Write-Output "Loaded $($keys.Count) keys. Testing..."

function Decrypt-AesCtr ($data, $keyBytes, $ivBytes) {
    $aes = [System.Security.Cryptography.Aes]::Create()
    $aes.Mode = [System.Security.Cryptography.CipherMode]::ECB
    $aes.Padding = [System.Security.Cryptography.PaddingMode]::None
    $aes.Key = $keyBytes
    $encryptor = $aes.CreateEncryptor()
    
    $counter = New-Object byte[] 16
    [Array]::Copy($ivBytes, $counter, 16)
    $keyStream = $encryptor.TransformFinalBlock($counter, 0, 16)
    
    $dec = New-Object byte[] 16
    for ($i = 0; $i -lt 16; $i++) {
        $dec[$i] = $data[$i] -bxor $keyStream[$i]
    }
    return $dec
}

foreach ($k in $keys.Keys) {
    $keyHex = $keys[$k]
    $keyBytes = New-Object byte[] 16
    for ($i = 0; $i -lt 16; $i++) {
        $keyBytes[$i] = [Convert]::ToByte($keyHex.Substring($i * 2, 2), 16)
    }
    
    # Try global block index
    $dec = Decrypt-AesCtr $encData $keyBytes $iv
    $magic = [System.Text.Encoding]::ASCII.GetString($dec, 0, 4)
    if ($magic -eq "PFS0") {
        Write-Output "FOUND KEY (Global Offset): $k = $keyHex"
        break
    }
    
    # Try relative block index = 0x6800
    $ivRel = New-Object byte[] 16
    [Array]::Copy($gen, 0, $ivRel, 0, 8)
    $blockBytesRel = [BitConverter]::GetBytes([long]($pfsOffset / 16))
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($blockBytesRel) }
    [Array]::Copy($blockBytesRel, 0, $ivRel, 8, 8)
    $decRel = Decrypt-AesCtr $encData $keyBytes $ivRel
    $magicRel = [System.Text.Encoding]::ASCII.GetString($decRel, 0, 4)
    if ($magicRel -eq "PFS0") {
        Write-Output "FOUND KEY (Relative Offset): $k = $keyHex"
        break
    }
}

Write-Output "Testing finished."
